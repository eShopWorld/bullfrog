using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Client;
using Client.Models;
using Eshopworld.Tests.Core;
using FluentAssertions;
using Newtonsoft.Json;
using Xunit;

public class MaxScaleTests : BaseApiTests
{
    [Fact, IsLayer0]
    public async Task MaxScaleOneEvent()
    {
        await ValidateScaleRestrictions(true, (0, 2, 0, 400));
    }

    [Fact, IsLayer0]
    public async Task TooLargeOneEvent()
    {
        await ValidateScaleRestrictions(false, (0, 2, 301, 0));
    }

    [Fact, IsLayer0]
    public async Task MaxScaleConsuctiveEvents()
    {
        await ValidateScaleRestrictions(true, (0, 2, 0, 300), (2, 3, 0, 400));
    }

    [Fact, IsLayer0]
    public async Task TwoNonDistinctEvents()
    {
        await ValidateScaleRestrictions(true, (5, 6, 0, 300), (2, 3, 0, 400));
    }

    [Fact, IsLayer0]
    public async Task MaxScaleOverlapingDifferentRegionsEvents()
    {
        await ValidateScaleRestrictions(true, (0, 2, 300, 0), (1, 3, 0, 400));
    }

    [Fact, IsLayer0]
    public async Task OverlapingEvents1()
    {
        await ValidateScaleRestrictions(false, (0, 2, 100, 0), (1, 3, 201, 0));
    }

    [Fact, IsLayer0]
    public async Task OverlapingEvents2()
    {
        await ValidateScaleRestrictions(false, (0, 5, 100, 0), (1, 3, 201, 0));
    }

    [Fact, IsLayer0]
    public async Task OverlapingEvents3()
    {
        await ValidateScaleRestrictions(false, (1, 2, 100, 0), (0, 3, 201, 0));
    }

    [Fact, IsLayer0]
    public async Task OverlapingEvents4()
    {
        await ValidateScaleRestrictions(false, (1, 3, 100, 0), (0, 2, 201, 0));
    }

    private async Task ValidateScaleRestrictions(bool isValid, params (int start, int end, int region1Scale, int region2Scale)[] events)
    {
        RegisterDefaultScalers();
        await CreateScaleGroup();
        var allEventsButLastOne = events.ToList();
        allEventsButLastOne.RemoveAt(allEventsButLastOne.Count - 1);
        foreach (var (start, end, region1Scale, region2Scale) in allEventsButLastOne)
        {
            await SaveScaleEvent(start, end, region1Scale, region2Scale);
        }
        var lastEvent = events.Last();

        Func<Task> call = () => SaveScaleEvent(lastEvent.start, lastEvent.end, lastEvent.region1Scale, lastEvent.region2Scale);

        if (isValid)
        {
            await call();
        }
        else
        {
            ShouldThrowBullfrogError(call, -2);
        }
    }

    private async Task SaveScaleEvent(int start, int end, int region1Scale, int region2Scale)
    {
        var regionScales = new[] { (name: "eu1", scale: region1Scale), (name: "eu2", scale: region2Scale) }
                        .Where(x => x.scale > 0);
        await SaveScaleEvent(start, end, regionScales);
    }

    private ScaleEvent NewScaleEvent(int scaleOut, int scaleIn, IEnumerable<(string regionName, int scale)> regions)
    {
        return new ScaleEvent
        {
            Name = "aa",
            RegionConfig = regions.Select(r => new RegionScaleValue(r.regionName, r.scale)).ToList(),
            RequiredScaleAt = UtcNow + TimeSpan.FromHours(scaleOut),
            StartScaleDownAt = UtcNow + TimeSpan.FromHours(scaleIn),
        };
    }

    private async Task SaveScaleEvent(int scaleOut, int scaleIn, IEnumerable<(string regionName, int scale)> regions, Guid? id = default)
    {
        await ApiClient.SaveScaleEventAsync("sg", id ?? Guid.NewGuid(), NewScaleEvent(scaleOut, scaleIn, regions));
    }

    private async Task CreateScaleGroup()
    {
        await ApiClient.SetDefinitionAsync("sg", NewScaleGroupDefinition());
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
                    Cosmos = new List<CosmosConfiguration>(),
                    ScaleSets = new List<ScaleSetConfiguration>
                    {
                        new ScaleSetConfiguration
                        {
                            Name = "s",
                            AutoscaleSettingsResourceId = GetAutoscaleSettingResourceId(),
                            ProfileName = "pr",
                            LoadBalancerResourceId = GetLoadBalancerResourceId(),
                            HealthPortPort = 9999,
                            DefaultInstanceCount = 1,
                            MinInstanceCount = 1,
                            RequestsPerInstance = 100,
                        },
                    },
                    MaxScale = 300,
                },
                new ScaleGroupRegion
                {
                    RegionName = "eu2",
                    Cosmos = new List<CosmosConfiguration>(),
                    ScaleSets = new List<ScaleSetConfiguration>
                    {
                        new ScaleSetConfiguration
                        {
                            Name = "s",
                            AutoscaleSettingsResourceId = GetAutoscaleSettingResourceId(),
                            ProfileName = "pr",
                            LoadBalancerResourceId = GetLoadBalancerResourceId(),
                            HealthPortPort = 9999,
                            DefaultInstanceCount = 1,
                            MinInstanceCount = 1,
                            RequestsPerInstance = 100,
                        },
                    },
                    MaxScale = 400,
                },
            },
        };
    }
}
