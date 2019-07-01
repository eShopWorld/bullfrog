using System;
using System.Threading.Tasks;
using Autofac;
using Bullfrog.Common.Cosmos;
using Bullfrog.Common.Models;
using Eshopworld.Tests.Core;
using FluentAssertions;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Xunit;

[Collection(CosmosFixtureCollection.TestFixtureName)]
public class ControlPlaneCosmosThroughputClientTests
{
    private readonly CosmosFixture _cosmosFixture;
    private readonly IResourceManagementClient _client;

    public ControlPlaneCosmosThroughputClientTests(CosmosFixture cosmosFixture)
    {
        _cosmosFixture = cosmosFixture;
        _client = _cosmosFixture.ComponentContext.Resolve<IResourceManagementClient>();
    }

    [Fact, IsLayer2]
    public async Task GetThroughputTest()
    {
        var throughput = await _client.GetThroughput(await GetConnection());

        throughput.Should().BeGreaterOrEqualTo(400);
    }

    [Fact, IsLayer2]
    public async Task SetTooLowThroughputTest()
    {
        var connection = await GetConnection();

        Func<Task> action = () => _client.SetThroughput(300, connection);

        action.Should().Throw<ThroughputOutOfRangeException>()
            .Which.MinimumThroughput.Should().BeGreaterOrEqualTo(400);
    }

    [Fact, IsLayer2]
    public async Task SetThroughputTest()
    {
        var connection = await GetConnection();
        var throughput = await _client.GetThroughput(connection);

        var newThroughput = await _client.SetThroughput(throughput + 100, connection);

        newThroughput.Should().Be(throughput + 100);
        var finalThroughput = await _client.GetThroughput(connection);
        finalThroughput.Should().Be(newThroughput);
    }

    [Fact, IsLayer2]
    public async Task ResetThroughputTest()
    {
        var connection = await GetConnection();
        var newThroughput1 = await _client.SetThroughput(800, connection);
        var changedThroughput1 = await _client.GetThroughput(connection);
        var newThroughput2 = await _client.SetThroughput(400, connection);
        var changedThroughput2 = await _client.GetThroughput(connection);

        newThroughput1.Should().Be(800);
        changedThroughput1.Should().Be(800);
        newThroughput2.Should().Be(400);
        changedThroughput2.Should().Be(400);
    }

    private async Task<CosmosDbControlPlaneConnection> GetConnection()
    {
        var cosmos = await _cosmosFixture.GetTestCosmos();
        return new CosmosDbControlPlaneConnection
        {
            AccountResurceId = cosmos.AccountResourceId,
            DatabaseName = cosmos.DatabaseName,
        };
    }
}
