using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Bullfrog.DomainEvents;
using Client;
using Client.Models;
using Eshopworld.Tests.Core;
using FluentAssertions;
using Moq;
using Xunit;

public class MultiRegionDomainEventTests : BaseApiTests
{
    [Fact, IsLayer0]
    public async Task ScaleOutStartedIsReported()
    {
        var start = UtcNow;
        CreateScaleGroup();
        var events = new List<(DateTimeOffset time, Guid id, ScaleChangeType type)>();
        BigBrotherMoq.Setup(x => x.Publish(It.IsAny<ScaleChange>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
            .Callback<ScaleChange, string, string, int>((sc, _, _x, _y) => events.Add((UtcNow, sc.Id, sc.Type)));
        ScaleSetMonitorMoq.Setup(x => x.GetNumberOfWorkingInstances("lb", 9999))
            .ReturnsAsync(() => UtcNow >= start.AddMinutes(30) ? 1 : 0);
        var eventId = AddEvent(10, 20);

        await AdvanceTimeTo(start.AddHours(25));

        var expected = new List<(DateTimeOffset time, Guid id, ScaleChangeType type)>
        {
            (start.AddHours(7), eventId, ScaleChangeType.ScaleOutStarted),
            (start.AddHours(10), eventId, ScaleChangeType.ScaleOutComplete),
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
                        AccountName = "ac",
                        DatabaseName = "dn",
                        MaximumRU = 1000,
                        MinimumRU = 400,
                        RequestUnitsPerRequest = 10,
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
                            AutoscaleSettingsResourceId = "ri",
                            ProfileName = "pr",
                            LoadBalancerResourceId = "lb",
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
                    ScaleSets = new List<ScaleSetConfiguration>
                    {
                        new ScaleSetConfiguration
                        {
                            Name = "s2",
                            AutoscaleSettingsResourceId = "ri",
                            ProfileName = "pr",
                            LoadBalancerResourceId = "lb",
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
                    ScaleSets = new List<ScaleSetConfiguration>
                    {
                        new ScaleSetConfiguration
                        {
                            Name = "s3",
                            AutoscaleSettingsResourceId = "ri",
                            ProfileName = "pr",
                            LoadBalancerResourceId = "lb",
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
