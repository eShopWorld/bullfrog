using System;
using System.Threading.Tasks;
using Bullfrog.Actors.Interfaces.Models;
using Bullfrog.Actors.ResourceScalers;
using Bullfrog.Common.Cosmos;
using Eshopworld.Core;
using Eshopworld.Tests.Core;
using FluentAssertions;
using Moq;
using Xunit;

public class CosmosScalerTests
{
    private readonly Mock<IBigBrother> BigBrotherMoq = new Mock<IBigBrother>();

    [Theory, IsLayer0]
    [InlineData(null)]
    [InlineData(20)]
    public async Task SetsReportedMinimumThroughput(int? newThroughput)
    {
        var cosmosThroughputClinetMoq = new Mock<ICosmosThroughputClient>(MockBehavior.Strict);
        cosmosThroughputClinetMoq.Setup(x => x.Get())
            .ReturnsAsync(new CosmosThroughput { MinimalRequestUnits = 400, RequestsUnits = 600 });
        cosmosThroughputClinetMoq.Setup(x => x.Set(400))
            .ReturnsAsync(new CosmosThroughput { RequestsUnits = 400 });
        var configuration = new CosmosConfiguration
        {
            AccountName = "a",
            MinimumRU = 300,
            MaximumRU = 800,
            RequestUnitsPerRequest = 10,
        };
        var scaler = new CosmosScaler(cosmosThroughputClinetMoq.Object, configuration, BigBrotherMoq.Object);

        var result = newThroughput.HasValue
            ? await scaler.ScaleOut(newThroughput.Value, DateTimeOffset.MaxValue)
            : await scaler.ScaleIn();

        result.Should().Be(40);
    }

    [Theory, IsLayer0]
    [InlineData(null)]
    [InlineData(20)]
    public async Task SetsConfiguredMinimumThroughput(int? newThroughput)
    {
        var cosmosThroughputClinetMoq = new Mock<ICosmosThroughputClient>(MockBehavior.Strict);
        cosmosThroughputClinetMoq.Setup(x => x.Get())
            .ReturnsAsync(new CosmosThroughput { MinimalRequestUnits = 200, RequestsUnits = 600 });
        cosmosThroughputClinetMoq.Setup(x => x.Set(300))
            .ReturnsAsync(new CosmosThroughput { RequestsUnits = 300 });
        var configuration = new CosmosConfiguration
        {
            AccountName = "a",
            MinimumRU = 300,
            MaximumRU = 800,
            RequestUnitsPerRequest = 10,
        };
        var scaler = new CosmosScaler(cosmosThroughputClinetMoq.Object, configuration, BigBrotherMoq.Object);

        var result = newThroughput.HasValue
           ? await scaler.ScaleOut(newThroughput.Value, DateTimeOffset.MaxValue)
           : await scaler.ScaleIn();

        result.Should().Be(30);
    }

    [Theory, IsLayer0]
    [InlineData(null)]
    [InlineData(20)]
    [InlineData(2000)]
    public async Task WaitsIfPreviousOperationIsInProgress(int? newThroughput)
    {
        var cosmosThroughputClinetMoq = new Mock<ICosmosThroughputClient>(MockBehavior.Strict);
        cosmosThroughputClinetMoq.Setup(x => x.Get())
            .ReturnsAsync(new CosmosThroughput { IsThroughputChangePending = true, MinimalRequestUnits = 200, RequestsUnits = 600 });
        // The Set method is not called
        var configuration = new CosmosConfiguration
        {
            AccountName = "a",
            MinimumRU = 300,
            MaximumRU = 800,
            RequestUnitsPerRequest = 10,
        };
        var scaler = new CosmosScaler(cosmosThroughputClinetMoq.Object, configuration, BigBrotherMoq.Object);

        var result = newThroughput.HasValue
        ? await scaler.ScaleOut(newThroughput.Value, DateTimeOffset.MaxValue)
        : await scaler.ScaleIn();

        result.Should().BeNull();
    }

    [Theory, IsLayer0]
    [InlineData(null, 300)]
    [InlineData(40, 400)]
    [InlineData(41, 500)]
    [InlineData(71, 800)]
    [InlineData(80, 800)]
    [InlineData(81, 800)]
    [InlineData(90, 800)]
    public async Task SetRequestUnitsIsCorrectlyRounded(int? newThroughput, int setValue)
    {
        var cosmosThroughputClinetMoq = new Mock<ICosmosThroughputClient>(MockBehavior.Strict);
        cosmosThroughputClinetMoq.Setup(x => x.Get())
            .ReturnsAsync(new CosmosThroughput { MinimalRequestUnits = 200, RequestsUnits = 600 });
        cosmosThroughputClinetMoq.Setup(x => x.Set(setValue))
            .ReturnsAsync(new CosmosThroughput { RequestsUnits = setValue });
        var configuration = new CosmosConfiguration
        {
            AccountName = "a",
            MinimumRU = 300,
            MaximumRU = 800,
            RequestUnitsPerRequest = 10,
        };
        var scaler = new CosmosScaler(cosmosThroughputClinetMoq.Object, configuration, BigBrotherMoq.Object);

        var result = newThroughput.HasValue
       ? await scaler.ScaleOut(newThroughput.Value, DateTimeOffset.MaxValue)
       : await scaler.ScaleIn();

        result.Should().Be(setValue / 10);
    }

    [Fact, IsLayer0]
    public async Task SetThroughputNotCompleted()
    {
        var cosmosThroughputClinetMoq = new Mock<ICosmosThroughputClient>(MockBehavior.Strict);
        cosmosThroughputClinetMoq.Setup(x => x.Get())
            .ReturnsAsync(new CosmosThroughput { MinimalRequestUnits = 200, RequestsUnits = 600 });
        cosmosThroughputClinetMoq.Setup(x => x.Set(500))
            .ReturnsAsync(new CosmosThroughput { IsThroughputChangePending = true, RequestsUnits = 500 });
        var configuration = new CosmosConfiguration
        {
            AccountName = "a",
            MinimumRU = 300,
            MaximumRU = 800,
            RequestUnitsPerRequest = 10,
        };
        var scaler = new CosmosScaler(cosmosThroughputClinetMoq.Object, configuration, BigBrotherMoq.Object);

        var result = await scaler.ScaleOut(50, DateTimeOffset.MaxValue);

        result.Should().BeNull();
    }

    [Fact, IsLayer0]
    public async Task SettingTheSameValue()
    {
        var cosmosThroughputClinetMoq = new Mock<ICosmosThroughputClient>(MockBehavior.Strict);
        cosmosThroughputClinetMoq.Setup(x => x.Get())
            .ReturnsAsync(new CosmosThroughput { MinimalRequestUnits = 200, RequestsUnits = 600 });
        var configuration = new CosmosConfiguration
        {
            AccountName = "a",
            MinimumRU = 300,
            MaximumRU = 800,
            RequestUnitsPerRequest = 10,
        };
        var scaler = new CosmosScaler(cosmosThroughputClinetMoq.Object, configuration, BigBrotherMoq.Object);

        var result = await scaler.ScaleOut(60, DateTimeOffset.MaxValue);

        result.Should().Be(60);
    }
}
