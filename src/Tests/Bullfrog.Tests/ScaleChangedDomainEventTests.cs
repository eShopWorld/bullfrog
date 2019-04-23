using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bullfrog.DomainEvents;
using Client;
using Client.Models;
using Eshopworld.Tests.Core;
using FluentAssertions;
using Moq;
using Xunit;

[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Minor Code Smell", "S3236:Caller information arguments should not be provided explicitly", Justification = "Moq requires all arguments.")]

public class ScaleChangedDomainEventTests : BaseApiTests
{
    private TimeSpan _cosmosDbPrescaleLeadTime = TimeSpan.FromMinutes(5);
    private TimeSpan _scaleSetPrescaleLeadTime = TimeSpan.FromMinutes(1);
    private TimeSpan MaxLeadTime => _cosmosDbPrescaleLeadTime < _scaleSetPrescaleLeadTime
        ? _scaleSetPrescaleLeadTime
        : _cosmosDbPrescaleLeadTime;

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
        var eventId = AddEvent(start.AddMinutes(30), start.AddMinutes(40), 10);

        await AdvanceTimeTo(start + TimeSpan.FromHours(1));

        var expected = new List<(DateTimeOffset time, Guid id, ScaleChangeType type)>
        {
            (start.AddMinutes(30).Add(-MaxLeadTime), eventId, ScaleChangeType.ScaleOutStarted),
            (start.AddMinutes(30), eventId, ScaleChangeType.ScaleOutComplete),
            (start.AddMinutes(40), eventId, ScaleChangeType.ScaleInStarted),
            (start.AddMinutes(40), eventId, ScaleChangeType.ScaleInComplete),
        };
        events.Should().BeEquivalentTo(expected);
    }

    [Fact, IsLayer0]
    public async Task MultipleEvent()
    {
        var start = UtcNow;
        CreateScaleGroup();
        var events = new List<(DateTimeOffset time, Guid id, ScaleChangeType type)>();
        BigBrotherMoq.Setup(x => x.Publish(It.IsAny<ScaleChange>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
            .Callback<ScaleChange, string, string, int>((sc, _, _x, _y) => events.Add((UtcNow, sc.Id, sc.Type)));
        var availableVMs = new (DateTimeOffset When, int Number)[]
        {
            (start, 0),
            (start.AddMinutes(18), 1),
            (start.AddMinutes(21), 2),
        };
        ScaleSetMonitorMoq.Setup(x => x.GetNumberOfWorkingInstances("lb", 9999))
            .ReturnsAsync(() => availableVMs.Last(x => x.When <= UtcNow).Number);
        var eventId1 = AddEvent(start.AddMinutes(20), start.AddMinutes(40), 80);
        var eventId2 = AddEvent(start.AddMinutes(22), start.AddMinutes(50), 30);

        await AdvanceTimeTo(start + TimeSpan.FromHours(1));

        var expected = new List<(DateTimeOffset time, Guid id, ScaleChangeType type)>
        {
            (start.AddMinutes(20).Add(-MaxLeadTime), eventId1, ScaleChangeType.ScaleOutStarted),
            (start.AddMinutes(20), eventId1, ScaleChangeType.ScaleOutComplete),
            (start.AddMinutes(40), eventId1, ScaleChangeType.ScaleInStarted),
            (start.AddMinutes(40), eventId1, ScaleChangeType.ScaleInComplete),
            (start.AddMinutes(22).Add(-MaxLeadTime), eventId2, ScaleChangeType.ScaleOutStarted),
            (start.AddMinutes(21), eventId2, ScaleChangeType.ScaleOutComplete),
            (start.AddMinutes(50), eventId2, ScaleChangeType.ScaleInStarted),
            (start.AddMinutes(50), eventId2, ScaleChangeType.ScaleInComplete),
        };
        events.Should().BeEquivalentTo(expected);
    }

    private Guid AddEvent(DateTimeOffset starts, DateTimeOffset ends, int scale)
    {
        var scaleEvent = new ScaleEvent
        {
            Name = "aa",
            RegionConfig = new List<RegionScaleValue>
            {
                new RegionScaleValue
                {
                    Name = "eu",
                    Scale = scale,
                }
            },
            RequiredScaleAt = starts,
            StartScaleDownAt = ends,
        };
        var eventId = Guid.NewGuid();
        ApiClient.SaveScaleEvent("sg", eventId, scaleEvent);
        return eventId;
    }

    private void CreateScaleGroup()
    {
        ApiClient.SetDefinition("sg", new ScaleGroupDefinition
        {
            Regions = new List<ScaleGroupRegion>
            {
                new ScaleGroupRegion
                {
                    RegionName = "eu",
                    Cosmos = new List<CosmosConfiguration>()
                    {
                        new CosmosConfiguration
                        {
                            Name = "c",
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
                            Name = "s",
                            AutoscaleSettingsResourceId = "ri",
                            ProfileName = "pr",
                            LoadBalancerResourceId = "lb",
                            HealthPortPort = 9999,
                            DefaultInstanceCount = 1,
                            MinInstanceCount = 1,
                            RequestsPerInstance = 100,
                        },
                    },
                    CosmosDbPrescaleLeadTime = _cosmosDbPrescaleLeadTime.ToString(),
                    ScaleSetPrescaleLeadTime = _scaleSetPrescaleLeadTime.ToString(),
                }
            },
        });
    }
}
