using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bullfrog.Actors.EventModels;
using Bullfrog.DomainEvents;
using Client;
using Client.Models;
using Eshopworld.Tests.Core;
using FluentAssertions;
using Xunit;

public class MultiRegionDomainEventTests : BaseApiTests
{
    private const string SharedCosmosRegion = Bullfrog.Actors.Interfaces.Models.ScaleGroupDefinition.SharedCosmosRegion;

    [Fact, IsLayer0]
    public async Task ScaleOutStartedIsReported()
    {
        var start = UtcNow;
        CreateScaleGroup();
        var eventId = AddEvent(10, 20);
        var eventStart = UtcNow.AddHours(10);
        RegisterResourceScaler("c1", x => UtcNow >= eventStart.AddHours(-0.25) ? (x ?? 10) : (int?)null);
        RegisterResourceScaler("s1", x => UtcNow >= eventStart.AddHours(-0.5) ? (x ?? 10) : (int?)null);
        RegisterResourceScaler("s2", x => UtcNow >= eventStart.AddHours(-1) ? (x ?? 10) : (int?)null);
        RegisterResourceScaler("s3", x => UtcNow >= eventStart.AddHours(-1.5) ? (x ?? 10) : (int?)null);

        await AdvanceTimeTo(start.AddHours(25));

        var events = GetPublishedEvents<ScaleChange>().Select(x => (x.Time, x.Event.Id, x.Event.Type));
        var expected = new List<(DateTimeOffset time, Guid id, ScaleChangeType type)>
        {
            (start.AddHours(7), eventId, ScaleChangeType.ScaleOutStarted),
            (RoundToScan(start.AddHours(9.75)), eventId, ScaleChangeType.ScaleOutComplete),
            (start.AddHours(20), eventId, ScaleChangeType.ScaleInStarted),
            (start.AddHours(20), eventId, ScaleChangeType.ScaleInComplete),
        };
        events.Should().BeEquivalentTo(expected);
    }

    [Fact, IsLayer0]
    public void ScaleChangeTypesAreOrderedCorrectly()
    {
        var validOrder = new[]
        {
            ScaleChangeType.ScaleIssue,
            ScaleChangeType.ScaleOutStarted,
            ScaleChangeType.ScaleOutComplete,
            ScaleChangeType.ScaleInStarted,
            ScaleChangeType.ScaleInComplete,
        };

        validOrder.Should().BeInAscendingOrder();
    }

    [Fact, IsLayer0]
    public async Task EventRegionStateChangesAreReported()
    {
        var start = StartTime;
        CreateScaleGroup();
        var eventId = AddEvent(10, 20);
        var eventStart = UtcNow.AddHours(10);
        RegisterResourceScaler("c1", x => UtcNow >= eventStart.AddHours(-0.25) ? (x ?? 10) : (int?)null);
        RegisterResourceScaler("s1", x => UtcNow >= eventStart.AddHours(-0.5) ? (x ?? 10) : (int?)null);
        RegisterResourceScaler("s2", x => UtcNow >= eventStart.AddHours(-1) ? (x ?? 10) : (int?)null);
        RegisterResourceScaler("s3", x => UtcNow >= eventStart.AddHours(-1.5) ? (x ?? 10) : (int?)null);

        await AdvanceTimeTo(start.AddHours(25));

        var events = GetPublishedEvents<EventRegionScaleChange>().Select(x => (x.Time, x.Event.Id, x.Event.Type, x.Event.RegionName));
        var expected = new List<(DateTimeOffset Time, Guid Id, ScaleChangeType Type, string Region)>
        {
            (start.AddHours(9), eventId, ScaleChangeType.ScaleOutStarted, "eu1"),
            (start.AddHours(8), eventId, ScaleChangeType.ScaleOutStarted, "eu2"),
            (start.AddHours(7), eventId, ScaleChangeType.ScaleOutStarted, "eu3"),
            (start.AddHours(9.5), eventId, ScaleChangeType.ScaleOutStarted,  SharedCosmosRegion),
            (start.AddHours(9.5), eventId, ScaleChangeType.ScaleOutComplete, "eu1"),
            (start.AddHours(9), eventId, ScaleChangeType.ScaleOutComplete, "eu2"),
            (start.AddHours(8.5), eventId, ScaleChangeType.ScaleOutComplete, "eu3"),
            (RoundToScan(start.AddHours(9.75)), eventId, ScaleChangeType.ScaleOutComplete, SharedCosmosRegion),
            (start.AddHours(20), eventId, ScaleChangeType.ScaleInStarted, "eu1"),
            (start.AddHours(20), eventId, ScaleChangeType.ScaleInStarted, "eu2"),
            (start.AddHours(20), eventId, ScaleChangeType.ScaleInStarted, "eu3"),
            (start.AddHours(20), eventId, ScaleChangeType.ScaleInStarted, SharedCosmosRegion),
            (start.AddHours(20), eventId, ScaleChangeType.ScaleInComplete, "eu1"),
            (start.AddHours(20), eventId, ScaleChangeType.ScaleInComplete, "eu2"),
            (start.AddHours(20), eventId, ScaleChangeType.ScaleInComplete, "eu3"),
            (start.AddHours(20), eventId, ScaleChangeType.ScaleInComplete, SharedCosmosRegion),
        };
        events.Should().BeEquivalentTo(expected);
    }

    private Guid AddEvent(int scaleOut = 10, int scaleIn = 20, IEnumerable<(string regionName, int scale)> regions = null)
    {
        var eventId = Guid.NewGuid();
        ApiClient.SaveScaleEvent("sg", eventId, NewScaleEvent(scaleOut, scaleIn, regions));
        return eventId;
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
            Cosmos = new List<CosmosConfiguration>()
                {
                    new CosmosConfiguration
                    {
                        Name = "c1",
                        MaximumRU = 1000,
                        MinimumRU = 400,
                        RequestUnitsPerRequest = 10,
                        DataPlaneConnection = new CosmosDbDataPlaneConnection
                        {
                            AccountName = "ac",
                            DatabaseName = "dn",
                        }
                    },
                },
            CosmosDbPrescaleLeadTime = TimeSpan.FromHours(0.5).ToString(),
            Regions = new List<ScaleGroupRegion>
            {
                new ScaleGroupRegion
                {
                    RegionName = "eu1",
                    ScaleSets = new List<ScaleSetConfiguration>
                    {
                        new ScaleSetConfiguration
                        {
                            Name = "s1",
                            AutoscaleSettingsResourceId = GetAutoscaleSettingResourceId(),
                            ProfileName = "pr",
                            LoadBalancerResourceId = GetLoadBalancerResourceId(),
                            HealthPortPort = 9999,
                            RequestsPerInstance = 100,
                        },
                    },
                    ScaleSetPrescaleLeadTime = TimeSpan.FromHours(1).ToString(),
                },
                new ScaleGroupRegion
                {
                    RegionName = "eu2",
                    ScaleSets = new List<ScaleSetConfiguration>
                    {
                        new ScaleSetConfiguration
                        {
                            Name = "s2",
                            AutoscaleSettingsResourceId = GetAutoscaleSettingResourceId(),
                            ProfileName = "pr",
                            LoadBalancerResourceId = GetLoadBalancerResourceId(),
                            HealthPortPort = 9999,
                            RequestsPerInstance = 100,
                        },
                    },
                    ScaleSetPrescaleLeadTime = TimeSpan.FromHours(2).ToString(),
                },
                new ScaleGroupRegion
                {
                    RegionName = "eu3",
                    ScaleSets = new List<ScaleSetConfiguration>
                    {
                        new ScaleSetConfiguration
                        {
                            Name = "s3",
                            AutoscaleSettingsResourceId = GetAutoscaleSettingResourceId(),
                            ProfileName = "pr",
                            LoadBalancerResourceId = GetLoadBalancerResourceId(),
                            HealthPortPort = 9999,
                            RequestsPerInstance = 100,
                        },
                    },
                    ScaleSetPrescaleLeadTime = TimeSpan.FromHours(3).ToString(),
                }
           },
        };
    }
}
