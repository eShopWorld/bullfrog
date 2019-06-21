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

    public ControlPlaneCosmosThroughputClientTests(CosmosFixture cosmosFixture)
    {
        _cosmosFixture = cosmosFixture;
    }

    [Fact, IsLayer2]
    public async Task GetThroughputTest()
    {
        var client = await CreateClient();

        var throughput = await client.Get();

        throughput.Should().BeGreaterOrEqualTo(400);
    }

    [Fact, IsLayer2]
    public async Task SetTooLowThroughputTest()
    {
        var client = await CreateClient();

        Func<Task> action = () => client.Set(300);

        action.Should().Throw<ThroughputOutOfRangeException>()
            .Which.MinimumThroughput.Should().BeGreaterOrEqualTo(400);
    }

    [Fact, IsLayer2]
    public async Task SetThroughputTest()
    {
        var client = await CreateClient();
        var throughput = await client.Get();

        var newThroughput = await client.Set(throughput + 100);

        newThroughput.Should().Be(throughput + 100);
        var finalThroughput = await client.Get();
        finalThroughput.Should().Be(newThroughput);
    }

    [Fact, IsLayer2]
    public async Task ResetThroughputTest()
    {
        var client = await CreateClient();

        var newThroughput1 = await client.Set(800);
        var changedThroughput1 = await client.Get();
        var newThroughput2 = await client.Set(400);
        var changedThroughput2 = await client.Get();

        newThroughput1.Should().Be(800);
        changedThroughput1.Should().Be(800);
        newThroughput2.Should().Be(400);
        changedThroughput2.Should().Be(400);
    }

    private async Task<ControlPlaneCosmosThroughputClient> CreateClient()
    {
        var cosmos = await _cosmosFixture.GetTestCosmos();
        var connection = new CosmosDbControlPlaneConnection
        {
            AccountResurceId = cosmos.AccountResourceId,
            DatabaseName = cosmos.DatabaseName,
        };
        var resourceManagementClient = _cosmosFixture.ComponentContext.Resolve<IResourceManagementClient>();
        return new ControlPlaneCosmosThroughputClient(connection, resourceManagementClient);
    }
}
