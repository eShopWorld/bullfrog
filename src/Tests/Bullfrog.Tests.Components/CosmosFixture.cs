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
    private readonly IConfigurationRoot _configuration;
    private readonly IContainer _container;
    private CosmosDetails _testCosmos;
    private CosmosClient _cosmosClient;

    public CosmosFixture()
    {
        _configuration = Eshopworld.DevOps.EswDevOpsSdk.BuildConfiguration();

        var builder = new ContainerBuilder();
        builder.RegisterModule<AzureManagementFluentModule>();
        _container = builder.Build();
    }

    public IComponentContext ComponentContext => _container;

    public async Task<CosmosDetails> GetTestCosmos()
    {
        if (_testCosmos != null)
            return _testCosmos;

        var accountResourceId = _configuration.GetSection("Bullfrog").GetSection("Testing")["TestCosmosAccountResourceId"];
        var azure = _container.Resolve<IAzure>();
        var account = await azure.CosmosDBAccounts.GetByIdAsync(accountResourceId);
        var connectionStrings = await account.ListConnectionStringsAsync();

        var databaseName = "TestDatabase1";
        var containerName = "TestContainer1";
        var connectionString = connectionStrings.ConnectionStrings.First().ConnectionString;
        _cosmosClient = new CosmosClient(connectionString);
        var databaseResponse = await _cosmosClient.Databases.CreateDatabaseIfNotExistsAsync(databaseName, 400);
        await databaseResponse.Database.Containers.CreateContainerIfNotExistsAsync(containerName, "/partitionKey");

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
        if (_testCosmos != null)
        {
            _cosmosClient.Databases[_testCosmos.DatabaseName].DeleteAsync().GetAwaiter().GetResult();
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
