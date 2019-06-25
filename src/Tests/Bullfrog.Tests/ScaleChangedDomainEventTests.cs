using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bullfrog.Common;
using Bullfrog.DomainEvents;
using Client;
using Client.Models;
using Eshopworld.Tests.Core;
using FluentAssertions;
using TestScalers;
using Xunit;

[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Minor Code Smell", "S3236:Caller information arguments should not be provided explicitly", Justification = "Moq requires all arguments.")]

public class ScaleChangedDomainEventTests : BaseApiTests
{
    private TimeSpan _cosmosDbPrescaleLeadTime;
    private TimeSpan _scaleSetPrescaleLeadTime;
    private TimeSpan MaxLeadTime => _cosmosDbPrescaleLeadTime < _scaleSetPrescaleLeadTime
        ? _scaleSetPrescaleLeadTime
        : _cosmosDbPrescaleLeadTime;

    [Fact, IsLayer0]
    public async Task ScaleOutStartedIsReported()
    {
        _cosmosDbPrescaleLeadTime = TimeSpan.FromMinutes(6);
        _scaleSetPrescaleLeadTime = TimeSpan.FromMinutes(2);
        CreateScaleGroup();
        RegisterResourceScaler("c", x => UtcNow >= StartTime.AddMinutes(30) ? 10 : (int?)null);
        RegisterResourceScaler("s", x => UtcNow >= StartTime.AddMinutes(30) ? 10 : (int?)null);
        var eventId = AddEvent(StartTime.AddMinutes(30), StartTime.AddMinutes(40), 10);

        await AdvanceTimeTo(StartTime + TimeSpan.FromHours(1));

        var events = GetPublishedEvents<ScaleChange>().Select(x => (x.Time, x.Event.Id, x.Event.Type));
        var expected = new List<(DateTimeOffset time, Guid id, ScaleChangeType type)>
        {
            (StartTime.AddMinutes(30).Add(-MaxLeadTime), eventId, ScaleChangeType.ScaleOutStarted),
            (StartTime.AddMinutes(30), eventId, ScaleChangeType.ScaleOutComplete),
            (StartTime.AddMinutes(40), eventId, ScaleChangeType.ScaleInStarted),
            (StartTime.AddMinutes(40), eventId, ScaleChangeType.ScaleInComplete),
        };
        events.Should().BeEquivalentTo(expected);
    }

    [Fact, IsLayer0]
    public async Task LimitedScaleOutIsReported()
    {
        _cosmosDbPrescaleLeadTime = TimeSpan.FromMinutes(6);
        _scaleSetPrescaleLeadTime = TimeSpan.FromMinutes(2);
        CreateScaleGroup();
        RegisterResourceScaler("c", x => UtcNow >= StartTime.AddMinutes(30) ? 10 : (int?)null);
        RegisterResourceScaler("s", x => UtcNow >= StartTime.AddMinutes(30) ? 5 : (int?)null);
        var eventId = AddEvent(StartTime.AddMinutes(30), StartTime.AddMinutes(40), 10);

        await AdvanceTimeTo(StartTime + TimeSpan.FromHours(1));

        var events = GetPublishedEvents<ScaleChange>().Select(x => (x.Time, x.Event.Id, x.Event.Type));
        var expected = new List<(DateTimeOffset time, Guid id, ScaleChangeType type)>
        {
            (StartTime.AddMinutes(30).Add(-MaxLeadTime), eventId, ScaleChangeType.ScaleOutStarted),
            (StartTime.AddMinutes(30), eventId, ScaleChangeType.ScaleIssue),
            (StartTime.AddMinutes(40), eventId, ScaleChangeType.ScaleInStarted),
            (StartTime.AddMinutes(40), eventId, ScaleChangeType.ScaleInComplete),
        };
        events.Should().BeEquivalentTo(expected);
    }

    [Fact, IsLayer0]
    public async Task ScaleOutIssuesBeforeStartAreReported()
    {
        _cosmosDbPrescaleLeadTime = TimeSpan.FromMinutes(16);
        _scaleSetPrescaleLeadTime = TimeSpan.FromMinutes(12);
        CreateScaleGroup();
        RegisterResourceScaler("c", x => UtcNow >= StartTime.AddMinutes(30) ? 10 : (int?)null);
        RegisterResourceScaler("s", x => UtcNow >= StartTime.AddMinutes(24) ? 10 : throw new BullfrogException());
        var eventId = AddEvent(StartTime.AddMinutes(30), StartTime.AddMinutes(60), 10);

        await AdvanceTimeTo(StartTime + TimeSpan.FromHours(1));

        var events = GetPublishedEvents<ScaleChange>().Select(x => (x.Time, x.Event.Id, x.Event.Type));
        var expected = new List<(DateTimeOffset time, Guid id, ScaleChangeType type)>
        {
            (StartTime.AddMinutes(30).Add(-MaxLeadTime), eventId, ScaleChangeType.ScaleOutStarted),
            (StartTime.AddMinutes(18), eventId, ScaleChangeType.ScaleIssue),
            (StartTime.AddMinutes(26), eventId, ScaleChangeType.ScaleOutStarted),
            (StartTime.AddMinutes(30), eventId, ScaleChangeType.ScaleOutComplete),
            (StartTime.AddMinutes(60), eventId, ScaleChangeType.ScaleInStarted),
            (StartTime.AddMinutes(60), eventId, ScaleChangeType.ScaleInComplete),
        };
        events.Should().BeEquivalentTo(expected);
    }

    [Fact, IsLayer0]
    public async Task ScaleOutIssuesDelayingStartAreReported()
    {
        _cosmosDbPrescaleLeadTime = TimeSpan.FromMinutes(6);
        _scaleSetPrescaleLeadTime = TimeSpan.FromMinutes(2);
        CreateScaleGroup();
        RegisterResourceScaler("c", x => UtcNow >= StartTime.AddMinutes(30) ? 10 : (int?)null);
        RegisterResourceScaler("s", x => UtcNow >= StartTime.AddMinutes(34) ? 10 : throw new BullfrogException());
        var eventId = AddEvent(StartTime.AddMinutes(30), StartTime.AddMinutes(60), 10);

        await AdvanceTimeTo(StartTime + TimeSpan.FromHours(1));

        var events = GetPublishedEvents<ScaleChange>().Select(x => (x.Time, x.Event.Id, x.Event.Type));
        var expected = new List<(DateTimeOffset time, Guid id, ScaleChangeType type)>
        {
            (StartTime.AddMinutes(30).Add(-MaxLeadTime), eventId, ScaleChangeType.ScaleOutStarted),
            (StartTime.AddMinutes(28), eventId, ScaleChangeType.ScaleIssue),
            (StartTime.AddMinutes(36), eventId, ScaleChangeType.ScaleOutComplete),
            (StartTime.AddMinutes(60), eventId, ScaleChangeType.ScaleInStarted),
            (StartTime.AddMinutes(60), eventId, ScaleChangeType.ScaleInComplete),
        };
        events.Should().BeEquivalentTo(expected);
    }

    [Fact, IsLayer0]
    public async Task MultipleEvents()
    {
        _cosmosDbPrescaleLeadTime = TimeSpan.FromMinutes(20);
        _scaleSetPrescaleLeadTime = TimeSpan.FromMinutes(14);
        var start = UtcNow;
        CreateScaleGroup();
        RegisterResourceScaler("c", new DelayTestScaler(TimeSpan.FromMinutes(8), 10, () => UtcNow));
        RegisterResourceScaler("s", new DelayTestScaler(TimeSpan.FromMinutes(6), 10, () => UtcNow));
        var eventId1 = AddEvent(start.AddMinutes(40), start.AddMinutes(100), 80);
        var eventId2 = AddEvent(start.AddMinutes(50), start.AddMinutes(110), 30);

        await AdvanceTimeTo(start + TimeSpan.FromHours(2));

        var events = GetPublishedEvents<ScaleChange>().Select(x => (x.Time, x.Event.Id, x.Event.Type));
        var expected = new List<(DateTimeOffset time, Guid id, ScaleChangeType type)>
        {
            (start.AddMinutes(20), eventId1, ScaleChangeType.ScaleOutStarted),
            (start.AddMinutes(42), eventId1, ScaleChangeType.ScaleOutComplete),
            (start.AddMinutes(100), eventId1, ScaleChangeType.ScaleInStarted),
            (start.AddMinutes(108), eventId1, ScaleChangeType.ScaleInComplete),
            (start.AddMinutes(30), eventId2, ScaleChangeType.ScaleOutStarted),
            (start.AddMinutes(42), eventId2, ScaleChangeType.ScaleOutComplete),
            (start.AddMinutes(110), eventId2, ScaleChangeType.ScaleInStarted),
            (start.AddMinutes(118), eventId2, ScaleChangeType.ScaleInComplete),
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
                            AutoscaleSettingsResourceId = GetAutoscaleSettingResourceId(),
                            ProfileName = "pr",
                            LoadBalancerResourceId = GetLoadBalancerResourceId(),
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
