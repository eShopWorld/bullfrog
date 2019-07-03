using System;
using System.Threading.Tasks;
using Bullfrog.Actors.Interfaces.Models;
using Bullfrog.Actors.ResourceScalers;
using Bullfrog.Common.Cosmos;
using Bullfrog.Common.Models;
using Eshopworld.Core;
using Eshopworld.Tests.Core;
using FluentAssertions;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Moq;
using Xunit;

public class ControlPlaneCosmosScalerTests
{
    [Fact, IsLayer0]
    public async Task SetTooLowThroughput()
    {
        var throughputClientMoq = CreateThroughputClientMoq();
        throughputClientMoq.Setup(x => x.Get()).ReturnsAsync(400);
        throughputClientMoq.Setup(x => x.Set(500)).ReturnsAsync(500);
        var cosmosConfiguration = new CosmosConfiguration
        {
            MinimumRU = 500,
            MaximumRU = 700,
            RequestUnitsPerRequest = 10,
        };
        var bigBrother = new Mock<IBigBrother>();
        var scaler = new ControlPlaneCosmosScaler(throughputClientMoq.Object, cosmosConfiguration, bigBrother.Object);

        var result = await scaler.ScaleOut(21, DateTimeOffset.MaxValue);

        result.Should().Be(50);
    }

    [Fact, IsLayer0]
    public async Task SetTooHighThroughput()
    {
        var throughputClientMoq = CreateThroughputClientMoq();
        throughputClientMoq.Setup(x => x.Get()).ReturnsAsync(400);
        throughputClientMoq.Setup(x => x.Set(700)).ReturnsAsync(700);
        var cosmosConfiguration = new CosmosConfiguration
        {
            MinimumRU = 500,
            MaximumRU = 700,
            RequestUnitsPerRequest = 10,
        };
        var bigBrother = new Mock<IBigBrother>();
        var scaler = new ControlPlaneCosmosScaler(throughputClientMoq.Object, cosmosConfiguration, bigBrother.Object);

        var result = await scaler.ScaleOut(72, DateTimeOffset.MaxValue);

        result.Should().Be(70);
    }

    [Fact, IsLayer0]
    public async Task SetThroughputIsRounded()
    {
        var throughputClientMoq = CreateThroughputClientMoq();
        throughputClientMoq.Setup(x => x.Get()).ReturnsAsync(400);
        throughputClientMoq.Setup(x => x.Set(600)).ReturnsAsync(600);
        var cosmosConfiguration = new CosmosConfiguration
        {
            MinimumRU = 500,
            MaximumRU = 700,
            RequestUnitsPerRequest = 10,
        };
        var bigBrother = new Mock<IBigBrother>();
        var scaler = new ControlPlaneCosmosScaler(throughputClientMoq.Object, cosmosConfiguration, bigBrother.Object);

        var result = await scaler.ScaleOut(55, DateTimeOffset.MaxValue);

        result.Should().Be(60);
    }

    [Fact, IsLayer0]
    public async Task SetThroughputLowerThanReported()
    {
        var throughputClientMoq = CreateThroughputClientMoq();
        throughputClientMoq.Setup(x => x.Get()).ReturnsAsync(600);
        throughputClientMoq.Setup(x => x.Set(400)).ThrowsAsync(
            new ThroughputOutOfRangeException(600, 10000, null));
        throughputClientMoq.Setup(x => x.Set(600)).ReturnsAsync(600);
        var cosmosConfiguration = new CosmosConfiguration
        {
            MinimumRU = 400,
            MaximumRU = 700,
            RequestUnitsPerRequest = 10,
        };
        var bigBrother = new Mock<IBigBrother>();
        var scaler = new ControlPlaneCosmosScaler(throughputClientMoq.Object, cosmosConfiguration, bigBrother.Object);

        var result = await scaler.ScaleOut(30, DateTimeOffset.MaxValue);

        result.Should().Be(60);
    }

    [Fact, IsLayer0]
    public async Task SetNotCalledWhenThroughputIsEqual()
    {
        var throughputClientMoq = CreateThroughputClientMoq();
        throughputClientMoq.Setup(x => x.Get()).ReturnsAsync(500);
        var cosmosConfiguration = new CosmosConfiguration
        {
            MinimumRU = 400,
            MaximumRU = 700,
            RequestUnitsPerRequest = 10,
        };
        var bigBrother = new Mock<IBigBrother>();
        var scaler = new ControlPlaneCosmosScaler(throughputClientMoq.Object, cosmosConfiguration, bigBrother.Object);

        var result = await scaler.ScaleOut(45, DateTimeOffset.MaxValue);

        result.Should().Be(50);
    }

    private static Mock<ControlPlaneCosmosThroughputClient> CreateThroughputClientMoq()
    {
        var resourceManagementClientMoq = new Mock<IResourceManagementClient>();
        var connection = new CosmosDbControlPlaneConnection
        {
            AccountResurceId = "subscriptions/79726995-901e-4020-85c6-b6401df55210/resourceGroups/rg/providers/Microsoft.DocumentDb/databaseAccounts/db"
        };
        var throughputClient = new Mock<ControlPlaneCosmosThroughputClient>(MockBehavior.Strict, connection, resourceManagementClientMoq.Object);
        return throughputClient;
    }
}
