using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Autofac;
using Bullfrog.Actors.Helpers;
using Bullfrog.Common;
using Bullfrog.Common.DependencyInjection;
using Eshopworld.Core;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace ExternalComponents
{
    public sealed class ComponentsTestFixture : IDisposable
    {
        private readonly IConfigurationRoot _configuration;
        private readonly HashSet<string> _temporaryCosmosDatabases = new HashSet<string>();
        private CosmosClient _cosmosClient;

        public IContainer Container { get; private set; }

        public Mock<IBigBrother> BigBrotherMock { get; private set; }

        public ComponentsTestFixture()
        {
            if (String.IsNullOrEmpty(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")))
                Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "CI");
            _configuration = Eshopworld.DevOps.EswDevOpsSdk.BuildConfiguration();
        }

        public void BuildContainer()
        {
            if (Container == null)
            {
                BigBrotherMock = new Mock<IBigBrother>();

                var builder = new ContainerBuilder();
                builder.RegisterInstance(_configuration)
                      .As<IConfigurationRoot>()
                      .SingleInstance();
                builder.RegisterModule<AzureManagementFluentModule>();
                builder.RegisterType<ScaleSetManager>().As<IScaleSetManager>();
                builder.RegisterType<CosmosManager>().As<ICosmosManager>();
                builder.RegisterInstance<IBigBrother>(BigBrotherMock.Object);
                Container = builder.Build();
            }
        }

        public CosmosClient GetCosmosClient()
        {
            if (_cosmosClient == null)
            {
                var configuration = Container.Resolve<IConfigurationRoot>();
                var cosmosConnectionString = configuration.GetCosmosAccountConnectionString("bullfrog-cosmos-test");
                _cosmosClient = new CosmosClient(cosmosConnectionString);
            }

            return _cosmosClient;
        }

        public async Task CreateTemporaryDatabaseIfNotExists(string name, int? thoughput)
        {
            _temporaryCosmosDatabases.Add(name);
            var client = GetCosmosClient();
            await client.Databases.CreateDatabaseIfNotExistsAsync(name, thoughput);
        }

        public void Dispose()
        {
            foreach(var db in _temporaryCosmosDatabases)
            {
                GetCosmosClient().Databases[db].DeleteAsync().GetAwaiter().GetResult();
            }
            _temporaryCosmosDatabases.Clear();
            _cosmosClient?.Dispose();
            Container?.Dispose();
        }
    }

    [CollectionDefinition(TestFixtureName, DisableParallelization = true)]
    public class ComponentsTestsCollection : ICollectionFixture<ComponentsTestFixture>
    {
        public const string TestFixtureName = "ComponentTests";
    }

}
