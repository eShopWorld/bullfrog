using System.Collections.Generic;
using System.Threading.Tasks;
using Bullfrog.Common.Cosmos;
using Bullfrog.Common.Models;
using Eshopworld.Tests.Core;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

[Collection(CosmosFixtureCollection.TestFixtureName)]
public class DataPlaneCosmosThroughputClientTests
{
    private readonly CosmosFixture _cosmosFixture;

    public DataPlaneCosmosThroughputClientTests(CosmosFixture cosmosFixture)
    {
        _cosmosFixture = cosmosFixture;
    }

    [Fact, IsLayer2]
    public async Task GetThroughput()
    {
        var client = await CreateClient();

        var throughput = await client.Get();

        throughput.Should().NotBeNull();
        throughput.MinimalRequestUnits.Should().BeGreaterOrEqualTo(400);
        throughput.RequestsUnits.Should().BeGreaterOrEqualTo(400);
        throughput.RequestsUnits.Should().BeGreaterOrEqualTo(throughput.MinimalRequestUnits);
    }


    [Fact, IsLayer2]
    public async Task SetThroughput()
    {
        var client = await CreateClient();
        var oldThroughput = await client.Get();
        var newThroughput = oldThroughput.RequestsUnits + (oldThroughput.RequestsUnits > oldThroughput.MinimalRequestUnits ? -100 : 100);

        var throughput = await client.Set(newThroughput);

        throughput.Should().NotBeNull();
        throughput.RequestsUnits.Should().Be(newThroughput);
        throughput.MinimalRequestUnits.Should().BeLessOrEqualTo(throughput.RequestsUnits);
        throughput.MinimalRequestUnits.Should().BeGreaterOrEqualTo(oldThroughput.MinimalRequestUnits);
    }

    private async Task<DataPlaneCosmosThroughputClient> CreateClient()
    {
        var cosmos = await _cosmosFixture.GetTestCosmos();
        var connection = new CosmosDbDataPlaneConnection
        {
            AccountName = "account1",
            DatabaseName = cosmos.DatabaseName,
        };
        var configurationBuilder = new ConfigurationBuilder();
        configurationBuilder.AddInMemoryCollection(new[]
        {
            new KeyValuePair<string, string>("Bullfrog:Cosmos:account1", cosmos.ConnectionString),
        });
        return new DataPlaneCosmosThroughputClient(connection, configurationBuilder.Build());
    }
}
