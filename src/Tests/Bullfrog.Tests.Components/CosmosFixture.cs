using System;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;
using Autofac;
using Bullfrog.Common.DependencyInjection;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Extensions.Configuration;
using Xunit;

public sealed class CosmosFixture : IDisposable
{
    private readonly IContainer _container;
    private CosmosDetails _testCosmos;
    private CosmosClient _cosmosClient;

    public CosmosFixture()
    {
        var builder = new ContainerBuilder();
        builder.RegisterModule<CoreModule>();
        builder.RegisterModule<AzureManagementFluentModule>();
        _container = builder.Build();
    }

    public IComponentContext ComponentContext => _container;

    public async Task<CosmosDetails> GetTestCosmos()
    {
        if (_testCosmos != null)
            return _testCosmos;

        var configuration = _container.Resolve<IConfigurationRoot>();
        var accountResourceId = configuration["Bullfrog:Testing:TestCosmosAccountResourceId"];
        var azure = _container.Resolve<IAzure>();
        var account = await azure.CosmosDBAccounts.GetByIdAsync(accountResourceId);
        var connectionStrings = await account.ListConnectionStringsAsync();

        var databaseName = "TestDatabase1";
        var containerName = "TestContainer1";
        var connectionString = connectionStrings.ConnectionStrings.First().ConnectionString;
        _cosmosClient = new CosmosClient(connectionString);
        var databaseResponse = await _cosmosClient.CreateDatabaseIfNotExistsAsync(databaseName, 400);
        await databaseResponse.Database.CreateContainerIfNotExistsAsync(containerName, "/partitionKey");

        _testCosmos = new CosmosDetails
        {
            AccountResourceId = accountResourceId,
            ConnectionString = connectionString,
            ContainerName = containerName,
            DatabaseName = databaseName,
        };

        return _testCosmos;
    }

    public static string ModifyConnectionString(string connectionString, string key, string newValue)
    {
        var builder = new DbConnectionStringBuilder
        {
            ConnectionString = connectionString,
        };
        builder[key] = newValue;
        return builder.ToString();
    }

    public void Dispose()
    {
        _container?.Dispose();
        if (_testCosmos != null)
        {
            _cosmosClient.GetDatabase(_testCosmos.DatabaseName).DeleteAsync().GetAwaiter().GetResult();
            _cosmosClient.Dispose();
        }
    }
}

public class CosmosDetails
{
    public string AccountResourceId { get; set; }

    public string ConnectionString { get; set; }

    public string DatabaseName { get; set; }

    public string ContainerName { get; set; }
}

[CollectionDefinition(TestFixtureName, DisableParallelization = true)]
public class CosmosFixtureCollection : ICollectionFixture<CosmosFixture>
{
    public const string TestFixtureName = "CosmosFixture";
}
