using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Client;
using Client.Models;
using Eshopworld.Tests.Core;
using FluentAssertions;
using Moq;
using Xunit;
using InternalScaleSetConfituration = Bullfrog.Actors.Interfaces.Models.ScaleSetConfiguration;

public class MultiRegionScaleGroupTests : BaseApiTests
{
    [Fact, IsLayer0]
    public async Task CreatingNewScaleEvent()
    {
        CreateScaleGroup();

        var response = await ApiClient.SaveScaleEventWithHttpMessagesAsync("sg", Guid.NewGuid(), NewScaleEvent());

        response.Response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Response.Headers.Location.Should().NotBeNull();
    }

    [Fact, IsLayer0]
    public async Task OnlySelectedRegionsShouldAffectPrescaleTime()
    {
        CreateScaleGroup();

        var savedScaleEvent = await ApiClient.SaveScaleEventAsync("sg", Guid.NewGuid(), NewScaleEvent(regions: new[] { ("eu1", 15), ("eu2", 45) }));

        savedScaleEvent.EstimatedScaleUpAt.Should().Be(UtcNow.AddHours(10 - 2));
    }

    [Fact, IsLayer0]
    public async Task GetScaleEventReturnsCorrectLeadTime()
    {
        CreateScaleGroup();
        var eventId = Guid.NewGuid();
        await ApiClient.SaveScaleEventAsync("sg", eventId, NewScaleEvent(regions: new[] { ("eu1", 15), ("eu2", 45) }));

        var savedScaleEvent = await ApiClient.GetScheduledEventAsync("sg", eventId);

        savedScaleEvent.EstimatedScaleUpAt.Should().Be(UtcNow.AddHours(10 - 2));
    }

    [Fact, IsLayer0]
    public async Task ListScaleEventsReturnsCorrectLeadTime()
    {
        CreateScaleGroup();
        await ApiClient.SaveScaleEventAsync("sg", Guid.NewGuid(), NewScaleEvent(regions: new[] { ("eu1", 15), ("eu2", 45) }));

        var savedScaleEvent = await ApiClient.ListScheduledEventsAsync("sg");

        savedScaleEvent.Should().HaveCount(1);
        savedScaleEvent[0].EstimatedScaleUpAt.Should().Be(UtcNow.AddHours(10 - 2));
    }

    [Fact, IsLayer0]
    public async Task DeleteScaleEventsReturnsState()
    {
        CreateScaleGroup();
        var eventId = Guid.NewGuid();
        await ApiClient.SaveScaleEventAsync("sg", eventId, NewScaleEvent(regions: new[] { ("eu1", 15), ("eu2", 45) }));
        await AdvanceTimeTo(UtcNow.AddHours(10 - 2.5));

        var response = await ApiClient.DeleteScaleEventWithHttpMessagesAsync("sg", eventId);

        response.Response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact, IsLayer0]
    public async Task RemovedRegionsShouldNotBeUsed()
    {
        CreateScaleGroup();
        var eventId = Guid.NewGuid();
        var scaleEvent = NewScaleEvent(regions: new[] { ("eu1", 15), ("eu2", 45) });
        await ApiClient.SaveScaleEventAsync("sg", eventId, scaleEvent);
        await AdvanceTimeTo(UtcNow.AddHours(1));
        ScaleSetManagerMoq.Setup(x => x.SetScale(It.IsAny<int>(), It.IsAny<InternalScaleSetConfituration>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(10);

        scaleEvent.RegionConfig.RemoveAt(1);
        await ApiClient.SaveScaleEventAsync("sg", eventId, scaleEvent);
        await AdvanceTimeTo(UtcNow.AddHours(30));

        ScaleSetManagerMoq.Verify(x => x.SetScale(45, It.IsAny<InternalScaleSetConfituration>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact, IsLayer0]
    public async Task AddedRegionsShouldBeUsed()
    {
        CreateScaleGroup();
        var eventId = Guid.NewGuid();
        var scaleEvent = NewScaleEvent(regions: new[] { ("eu1", 15) });
        await ApiClient.SaveScaleEventAsync("sg", eventId, scaleEvent);
        await AdvanceTimeTo(UtcNow.AddHours(1));
        ScaleSetManagerMoq.Setup(x => x.SetScale(It.IsAny<int>(), It.IsAny<InternalScaleSetConfituration>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(10);

        scaleEvent.RegionConfig.Add(new RegionScaleValue("eu2", 45));
        await ApiClient.SaveScaleEventAsync("sg", eventId, scaleEvent);
        await AdvanceTimeTo(UtcNow.AddHours(30));

        ScaleSetManagerMoq.Verify(x => x.SetScale(15, It.IsAny<InternalScaleSetConfituration>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    private ScaleEvent NewScaleEvent(int scaleOut = 10, int scaleIn = 20, IEnumerable<(string regionName, int scale)> regions = null)
    {
        if (regions == null)
            regions = new[] { ("eu1", 10), ("eu2", 20), ("eu3", 30) };
        return new ScaleEvent
        {
            Name = "aa",
            RegionConfig = regions.Select(r => new RegionScaleValue(r.regionName, r.scale)).ToList(),
            RequiredScaleAt = UtcNow + TimeSpan.FromHours(scaleOut),
            StartScaleDownAt = UtcNow + TimeSpan.FromHours(scaleIn),
        };
    }

    private void CreateScaleGroup(ScaleGroupDefinition scaleGroupDefinition = null)
    {
        ApiClient.SetDefinition("sg", scaleGroupDefinition ?? NewScaleGroupDefinition());
    }

    private static ScaleGroupDefinition NewScaleGroupDefinition()
    {
        return new ScaleGroupDefinition
        {
            Regions = new List<ScaleGroupRegion>
            {
                new ScaleGroupRegion
                {
                    RegionName = "eu1",
                    Cosmos = new List<CosmosConfiguration>()
                    {
                        new CosmosConfiguration
                        {
                            Name = "c1",
                            AccountName = "ac",
                            DatabaseName = "dn",
                            MaximumRU = 1000,
                            MinimumRU = 400,
                            RequestUnitsPerRequest = 10,
                        },
                    },
                    ScaleSets = new List<ScaleSetConfiguration>
                    {
                        new ScaleSetConfiguration
                        {
                            Name = "s1",
                            AutoscaleSettingsResourceId = GetAutoscaleSettingResourceId(),
                            ProfileName = "pr",
                            LoadBalancerResourceId = GetLoadBalancerResourceId(),
                            HealthPortPort = 9999,
                            DefaultInstanceCount = 1,
                            MinInstanceCount = 1,
                            RequestsPerInstance = 100,
                        },
                    },
                    ScaleSetPrescaleLeadTime = TimeSpan.FromHours(1).ToString(),
                },
                new ScaleGroupRegion
                {
                    RegionName = "eu2",
                    Cosmos = new List<CosmosConfiguration>()
                    {
                        new CosmosConfiguration
                        {
                            Name = "c2",
                            AccountName = "ac",
                            DatabaseName = "dn",
                            MaximumRU = 1000,
                            MinimumRU = 400,
                            RequestUnitsPerRequest = 10,
                        },
                    },
                    ScaleSets = new List<ScaleSetConfiguration>
                    {
                        new ScaleSetConfiguration
                        {
                            Name = "s2",
                            AutoscaleSettingsResourceId = GetAutoscaleSettingResourceId(),
                            ProfileName = "pr",
                            LoadBalancerResourceId = GetLoadBalancerResourceId(),
                            HealthPortPort = 9999,
                            DefaultInstanceCount = 1,
                            MinInstanceCount = 1,
                            RequestsPerInstance = 100,
                        },
                    },
                    ScaleSetPrescaleLeadTime = TimeSpan.FromHours(2).ToString(),
                },
                new ScaleGroupRegion
                {
                    RegionName = "eu3",
                    Cosmos = new List<CosmosConfiguration>()
                    {
                        new CosmosConfiguration
                        {
                            Name = "c3",
                            AccountName = "ac",
                            DatabaseName = "dn",
                            MaximumRU = 1000,
                            MinimumRU = 400,
                            RequestUnitsPerRequest = 10,
                        },
                    },
                    ScaleSets = new List<ScaleSetConfiguration>
                    {
                        new ScaleSetConfiguration
                        {
                            Name = "s3",
                            AutoscaleSettingsResourceId = GetAutoscaleSettingResourceId(),
                            ProfileName = "pr",
                            LoadBalancerResourceId = GetLoadBalancerResourceId(),
                            HealthPortPort = 9999,
                            DefaultInstanceCount = 1,
                            MinInstanceCount = 1,
                            RequestsPerInstance = 100,
                        },
                    },
                    ScaleSetPrescaleLeadTime = TimeSpan.FromHours(3).ToString(),
                }
           },
        };
    }
}
