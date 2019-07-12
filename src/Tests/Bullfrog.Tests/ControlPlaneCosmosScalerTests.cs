using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Bullfrog.Actors.Interfaces.Models;
using Bullfrog.Actors.ResourceScalers;
using Bullfrog.Common.Cosmos;
using Bullfrog.Common.Models;
using Eshopworld.Core;
using Eshopworld.Tests.Core;
using FluentAssertions;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Models;
using Microsoft.Rest.Azure;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;

public class ControlPlaneCosmosScalerTests
{
    private static CosmosDbControlPlaneConnection TestControlPlaneConnection => new CosmosDbControlPlaneConnection
    {
        AccountResurceId = "subscriptions/79726995-901e-4020-85c6-b6401df55210/resourceGroups/rg/providers/Microsoft.DocumentDb/databaseAccounts/db",
        DatabaseName = "db",
    };

    [Fact, IsLayer0]
    public async Task SetTooLowThroughput()
    {
        var throughputOperations = new Mock<IThroughputOperations>(MockBehavior.Strict);
        throughputOperations.Setup(x => x.Get()).Returns(400);
        throughputOperations.Setup(x => x.Set(500)).Returns(500);
        var cosmosConfiguration = new CosmosConfiguration
        {
            MinimumRU = 500,
            MaximumRU = 700,
            RequestUnitsPerRequest = 10,
            ControlPlaneConnection = TestControlPlaneConnection,
        };
        var bigBrother = new Mock<IBigBrother>();
        var scaler = new ControlPlaneCosmosScaler(CreateThroughputClientMoq(throughputOperations.Object), cosmosConfiguration, bigBrother.Object);

        var result = await scaler.ScaleOut(21, DateTimeOffset.MaxValue);

        result.Should().Be(50);
    }

    [Fact, IsLayer0]
    public async Task SetTooHighThroughput()
    {
        var throughputOperations = new Mock<IThroughputOperations>(MockBehavior.Strict);
        throughputOperations.Setup(x => x.Get()).Returns(400);
        throughputOperations.Setup(x => x.Set(700)).Returns(700);
        var cosmosConfiguration = new CosmosConfiguration
        {
            MinimumRU = 500,
            MaximumRU = 700,
            RequestUnitsPerRequest = 10,
            ControlPlaneConnection = TestControlPlaneConnection,
        };
        var bigBrother = new Mock<IBigBrother>();
        var scaler = new ControlPlaneCosmosScaler(CreateThroughputClientMoq(throughputOperations.Object), cosmosConfiguration, bigBrother.Object);

        var result = await scaler.ScaleOut(72, DateTimeOffset.MaxValue);

        result.Should().Be(70);
    }

    [Fact, IsLayer0]
    public async Task SetThroughputIsRounded()
    {
        var throughputOperations = new Mock<IThroughputOperations>(MockBehavior.Strict);
        throughputOperations.Setup(x => x.Get()).Returns(400);
        throughputOperations.Setup(x => x.Set(600)).Returns(600);
        var cosmosConfiguration = new CosmosConfiguration
        {
            MinimumRU = 500,
            MaximumRU = 700,
            RequestUnitsPerRequest = 10,
            ControlPlaneConnection = TestControlPlaneConnection,
        };
        var bigBrother = new Mock<IBigBrother>();
        var scaler = new ControlPlaneCosmosScaler(CreateThroughputClientMoq(throughputOperations.Object), cosmosConfiguration, bigBrother.Object);

        var result = await scaler.ScaleOut(55, DateTimeOffset.MaxValue);

        result.Should().Be(60);
    }

    [Fact, IsLayer0]
    public async Task SetThroughputLowerThanReported()
    {
        var throughputOperations = new Mock<IThroughputOperations>(MockBehavior.Strict);
        throughputOperations.Setup(x => x.Get()).Returns(600);
        throughputOperations.Setup(x => x.Set(400)).Throws(
            new ThroughputOutOfRangeException(600, 10000, null));
        throughputOperations.Setup(x => x.Set(600)).Returns(600);
        var cosmosConfiguration = new CosmosConfiguration
        {
            MinimumRU = 400,
            MaximumRU = 700,
            RequestUnitsPerRequest = 10,
            ControlPlaneConnection = TestControlPlaneConnection,
        };
        var bigBrother = new Mock<IBigBrother>();
        var scaler = new ControlPlaneCosmosScaler(CreateThroughputClientMoq(throughputOperations.Object), cosmosConfiguration, bigBrother.Object);

        var result = await scaler.ScaleOut(30, DateTimeOffset.MaxValue);

        result.Should().Be(60);
    }

    [Fact, IsLayer0]
    public async Task SetNotCalledWhenThroughputIsEqual()
    {
        var throughputOperations = new Mock<IThroughputOperations>(MockBehavior.Strict);
        throughputOperations.Setup(x => x.Get()).Returns(500);
        var cosmosConfiguration = new CosmosConfiguration
        {
            MinimumRU = 400,
            MaximumRU = 700,
            RequestUnitsPerRequest = 10,
            ControlPlaneConnection = TestControlPlaneConnection,
        };
        var bigBrother = new Mock<IBigBrother>();
        var scaler = new ControlPlaneCosmosScaler(CreateThroughputClientMoq(throughputOperations.Object), cosmosConfiguration, bigBrother.Object);

        var result = await scaler.ScaleOut(45, DateTimeOffset.MaxValue);

        result.Should().Be(50);
    }

    private static IResourceManagementClient CreateThroughputClientMoq(IThroughputOperations throughputOperations)
    {
        AzureOperationResponse<GenericResourceInner> ThroughputResponse(int throughput)
        {
            return new AzureOperationResponse<GenericResourceInner>()
            {
                Body = new GenericResourceInner(properties: JObject.FromObject(new { throughput }))
            };
        }

        int ReadNewThroughput(GenericResourceInner gri)
        {
            return (int)JObject.FromObject(gri.Properties)["resource"]["throughput"];
        }

        var resourceManagementClientMoq = new Mock<IResourceManagementClient>();
        var resourcesOperationsMoq = new Mock<IResourcesOperations>();
        resourcesOperationsMoq.Setup(x => x.GetByIdWithHttpMessagesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Dictionary<string, List<string>>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string r, string v, Dictionary<string, List<string>> d, CancellationToken ct) => ThroughputResponse(throughputOperations.Get()));
        resourcesOperationsMoq.Setup(x => x.CreateOrUpdateByIdWithHttpMessagesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<GenericResourceInner>(), It.IsAny<Dictionary<string, List<string>>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string r, string v, GenericResourceInner gri, Dictionary<string, List<string>> d, CancellationToken ct) => ThroughputResponse(throughputOperations.Set(ReadNewThroughput(gri))));

        resourceManagementClientMoq.SetupGet(x => x.Resources).Returns(resourcesOperationsMoq.Object);

        return resourceManagementClientMoq.Object;
    }

    public interface IThroughputOperations
    {
        int Get();
        int Set(int throughput);
    }
}
