using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Client;
using Client.Models;
using Eshopworld.Tests.Core;
using FluentAssertions;
using Moq;
using ServiceFabric.Mocks;
using Xunit;

public class ScaleManagerTests : BaseApiTests
{
    private TimeSpan _cosmosDbPrescaleLeadTime;
    private TimeSpan _scaleSetPrescaleLeadTime;

    [Fact, IsLayer0]
    public void AddingEventToUnknownScaleGroup()
    {
        var scaleEvent = new Client.Models.ScaleEvent
        {
            Name = "aa",
            RegionConfig = new List<RegionScaleValue>
            {
                new RegionScaleValue
                {
                    Name = "eu",
                    Scale = 10,
                }
            },
            RequiredScaleAt = DateTime.UtcNow + TimeSpan.FromHours(1),
            StartScaleDownAt = DateTime.UtcNow + TimeSpan.FromHours(2),
        };

        //act
        Func<Task> func = () => ApiClient.SaveScaleEventAsync("sg", Guid.NewGuid(), scaleEvent);

        func.Should().Throw<ProblemDetailsException>()
            .Where(x => x.Response.StatusCode == System.Net.HttpStatusCode.NotFound);
    }

    [Fact, IsLayer0]
    public void ListingEventsFromUnknownScaleGroup()
    {
        //act
        Func<Task> func = () => ApiClient.ListScheduledEventsAsync("sg");

        func.Should().Throw<ProblemDetailsException>()
            .Where(x => x.Response.StatusCode == System.Net.HttpStatusCode.NotFound);
    }

    [Fact, IsLayer0]
    public void DeletingEventsFromUnknownScaleGroup()
    {
        //act
        Func<Task> func = () => ApiClient.DeleteScaleEventAsync("sg", Guid.NewGuid());

        func.Should().Throw<ProblemDetailsException>()
            .Where(x => x.Response.StatusCode == System.Net.HttpStatusCode.NotFound);
    }


    [Fact, IsLayer0]
    public void GetEventFromUnknownScaleGroup()
    {
        //act
        Func<Task> func = () => ApiClient.GetScheduledEventAsync("sg", Guid.NewGuid());

        func.Should().Throw<ProblemDetailsException>()
            .Where(x => x.Response.StatusCode == System.Net.HttpStatusCode.NotFound);
    }

    [Fact, IsLayer0]
    public void GetUnknownEvent()
    {
        CreateScaleGroup();

        //act
        Action action = () => ApiClient.GetScheduledEvent("sg", Guid.NewGuid());

        action.Should().Throw<ProblemDetailsException>()
            .Where(x => x.Response.StatusCode == System.Net.HttpStatusCode.NotFound);
    }

    [Fact, IsLayer0]
    public void AddingEvent()
    {
        CreateScaleGroup();
        var scaleEvent = new ScaleEvent
        {
            Name = "aa",
            RegionConfig = new List<RegionScaleValue>
            {
                new RegionScaleValue
                {
                    Name = "eu",
                    Scale = 10,
                }
            },
            RequiredScaleAt = UtcNow + TimeSpan.FromHours(1),
            StartScaleDownAt = UtcNow + TimeSpan.FromHours(2),
        };
        var id = Guid.NewGuid();

        //act
        ApiClient.SaveScaleEvent("sg", id, scaleEvent);

        var scheduledEvent = ApiClient.GetScheduledEvent("sg", id);
        scheduledEvent.Should().BeEquivalentTo(scaleEvent);
        var allEvents = ApiClient.ListScheduledEvents("sg");
        allEvents.Should().BeEquivalentTo(new[] { scaleEvent });
        var actor = ScaleManagerActors[("sg", "eu")];
        var reminders = actor.GetActorReminders();
        reminders.Should().HaveCount(1);
    }

    [Fact, IsLayer0]
    public void ListReturnsOrderedScaleEvents()
    {
        CreateScaleGroup();
        var events = new (int start, int end)[] { (1, 2), (4, 6), (2, 8), (4, 5), };
        foreach (var rng in events)
        {
            var scaleEvent = new ScaleEvent
            {
                Name = "aa",
                RegionConfig = new List<RegionScaleValue>
            {
                new RegionScaleValue
                {
                    Name = "eu",
                    Scale = 10,
                }
            },
                RequiredScaleAt = UtcNow + TimeSpan.FromHours(rng.start),
                StartScaleDownAt = UtcNow + TimeSpan.FromHours(rng.end),
            };
            ApiClient.SaveScaleEvent("sg", Guid.NewGuid(), scaleEvent);
        }

        var allScaleEvents = ApiClient.ListScheduledEvents("sg");

        var returnedEventStartEnds = allScaleEvents.Select(ev => ToStartEnd(ev));
        var ordered = events.OrderBy(ev => ev.start).ThenBy(ev => ev.end);
        returnedEventStartEnds.Should().ContainInOrder(ordered);

        (int start, int end) ToStartEnd(ScheduledScaleEvent ev)
            => ((int)(ev.RequiredScaleAt.Value - UtcNow).TotalHours, (int)(ev.StartScaleDownAt.Value - UtcNow).TotalHours);
    }

    [Fact, IsLayer0]
    public async Task OnlyActiveEventsAreReturned()
    {
        CreateScaleGroup();
        var events = new (int start, int end)[] { (2, 8), (4, 5), (3, 7), (6, 7) };
        foreach (var rng in events)
        {
            var scaleEvent = new ScaleEvent
            {
                Name = "aa",
                RegionConfig = new List<RegionScaleValue>
                {
                    new RegionScaleValue
                    {
                        Name = "eu",
                        Scale = 10,
                    }
                },
                RequiredScaleAt = UtcNow + TimeSpan.FromHours(rng.start),
                StartScaleDownAt = UtcNow + TimeSpan.FromHours(rng.end),
            };
            ApiClient.SaveScaleEvent("sg", Guid.NewGuid(), scaleEvent);
        }
        await AdvanceTimeTo(UtcNow.AddHours(4));

        var allScaleEvents = ApiClient.ListScheduledEvents("sg", activeOnly: true);

        var returnedEventStartEnds = allScaleEvents.Select(ev => ToStartEnd(ev));
        var ordered = events
            .Select(ev => (start: ev.start - 4, end: ev.end - 4))
            .Where(ev => ev.end >= 0)
            .OrderBy(ev => ev.start)
            .ThenBy(ev => ev.end);
        returnedEventStartEnds.Should().ContainInOrder(ordered);

        (int start, int end) ToStartEnd(ScheduledScaleEvent ev)
            => ((int)(ev.RequiredScaleAt.Value - UtcNow).TotalHours, (int)(ev.StartScaleDownAt.Value - UtcNow).TotalHours);
    }

    [Fact, IsLayer0]
    public async Task ListReturnsEventsFromSelectedRegion()
    {
        CreateScaleGroup();
        var events = new (int start, int end, string regions)[] { (2, 8, "eu"), (4, 5, "eu1,eu"), (3, 7, "eu1"), (6, 7, "eu") };
        foreach (var rng in events)
        {
            var scaleEvent = new ScaleEvent
            {
                Name = "aa",
                RegionConfig = rng.regions.Split(',')
                    .Select(r => new RegionScaleValue { Name = r, Scale = 10 })
                    .ToList(),
                RequiredScaleAt = UtcNow + TimeSpan.FromHours(rng.start),
                StartScaleDownAt = UtcNow + TimeSpan.FromHours(rng.end),
            };
            ApiClient.SaveScaleEvent("sg", Guid.NewGuid(), scaleEvent);
        }

        var allScaleEvents = ApiClient.ListScheduledEvents("sg", fromRegion: "eu1");

        var returnedEventStartEnds = allScaleEvents.Select(ev => ToStartEnd(ev));
        var ordered = events.Where(ev => ev.regions.Contains("eu1"))
            .Select(ev=>(ev.start, ev.end))
            .OrderBy(ev => ev.start).ThenBy(ev => ev.end);
        returnedEventStartEnds.Should().ContainInOrder(ordered);

        (int start, int end) ToStartEnd(ScheduledScaleEvent ev)
            => ((int)(ev.RequiredScaleAt.Value - UtcNow).TotalHours, (int)(ev.StartScaleDownAt.Value - UtcNow).TotalHours);
    }

    [Fact, IsLayer0]
    public void AddingEventWithoutRegions()
    {
        CreateScaleGroup();
        var scaleEvent = new ScaleEvent
        {
            Name = "aa",
            RegionConfig = new List<RegionScaleValue>
            {
            },
            RequiredScaleAt = UtcNow + TimeSpan.FromHours(1),
            StartScaleDownAt = UtcNow + TimeSpan.FromHours(2),
        };
        var id = Guid.NewGuid();

        //act
        Action call = () => ApiClient.SaveScaleEvent("sg", id, scaleEvent);

        call.Should().Throw<ProblemDetailsException>()
            .Which.Body.AdditionalProperties.ContainsKey("regionConfig");
    }

    [Fact, IsLayer0]
    public async Task DeletingEvent()
    {
        CreateScaleGroup();
        var scaleEvent = new Client.Models.ScaleEvent
        {
            Name = "aa",
            RegionConfig = new List<RegionScaleValue>
            {
                new RegionScaleValue
                {
                    Name = "eu",
                    Scale = 10,
                }
            },
            RequiredScaleAt = UtcNow + TimeSpan.FromHours(1),
            StartScaleDownAt = UtcNow + TimeSpan.FromHours(2),
        };
        var id = Guid.NewGuid();
        ApiClient.SaveScaleEvent("sg", id, scaleEvent);

        //act
        ApiClient.DeleteScaleEvent("sg", id);
        await AdvanceTimeTo(UtcNow.AddMinutes(1));

        Action getEvent = () => ApiClient.GetScheduledEvent("sg", id);
        getEvent.Should().Throw<ProblemDetailsException>()
             .Where(x => x.Response.StatusCode == System.Net.HttpStatusCode.NotFound);
        var allEvents = ApiClient.ListScheduledEvents("sg");
        allEvents.Should().BeEmpty();
        var actor = ScaleManagerActors[("sg", "eu")];
        var reminders = actor.GetActorReminders();
        reminders.Should().BeEmpty();
    }

    [Fact, IsLayer0]
    public async Task AddingActiveEvent()
    {
        _cosmosDbPrescaleLeadTime = TimeSpan.FromMinutes(5);
        _scaleSetPrescaleLeadTime = TimeSpan.FromMinutes(5);
        CreateScaleGroup();
        var scaleEvent = new Client.Models.ScaleEvent
        {
            Name = "aa",
            RegionConfig = new List<RegionScaleValue>
            {
                new RegionScaleValue
                {
                    Name = "eu",
                    Scale = 10,
                }
            },
            RequiredScaleAt = UtcNow + TimeSpan.FromMinutes(1),
            StartScaleDownAt = UtcNow + TimeSpan.FromHours(2),
        };
        RegisterResourceScaler("c", x => x ?? 10);
        RegisterResourceScaler("s", x => x ?? 10);

        //act
        ApiClient.SaveScaleEvent("sg", Guid.NewGuid(), scaleEvent);
        await AdvanceTimeTo(UtcNow.AddMinutes(5));

        ScaleHistory["c"].Should().NotBeEmpty();
        ScaleHistory["s"].Should().NotBeEmpty();
    }

    [Fact, IsLayer0]
    public void CurrentStateWithoutActiveEvent()
    {
        CreateScaleGroup();

        var state = ApiClient.GetCurrentState("sg");

        state.Should().NotBeNull();
        state.Regions.Should().BeEmpty();
    }

    [Fact, IsLayer0]
    public async Task CurrentStateWithActiveEvent()
    {
        RegisterResourceScaler("s", x => x ?? 10);
        RegisterResourceScaler("c", x => x ?? 10);
        var start = UtcNow;
        _scaleSetPrescaleLeadTime = TimeSpan.FromMinutes(10);
        CreateScaleGroup();
        var scaleEvent = new ScaleEvent
        {
            Name = "aa",
            RegionConfig = new List<RegionScaleValue>
            {
                new RegionScaleValue
                {
                    Name = "eu",
                    Scale = 10,
                }
            },
            RequiredScaleAt = start + TimeSpan.FromMinutes(15),
            StartScaleDownAt = start + TimeSpan.FromHours(2),
        };
        ApiClient.SaveScaleEvent("sg", Guid.NewGuid(), scaleEvent);
        await AdvanceTimeTo(start + TimeSpan.FromMinutes(30));
        ScaleSetMonitorMoq.Setup(x => x.GetNumberOfWorkingInstances(GetLoadBalancerResourceId(), 9999))
            .ReturnsAsync(2);

        var state = ApiClient.GetCurrentState("sg");

        state.Should().NotBeNull();
        state.Regions.Should().HaveCount(1);
        state.Regions[0].Should().BeEquivalentTo(
            new ScaleRegionState
            {
                Name = "eu",
                WasScaledUpAt = start + TimeSpan.FromMinutes(5),
                WillScaleDownAt = start + TimeSpan.FromHours(2),
                Scale = 200,
                RequestedScale = 10,
                ScaleSetState = new Dictionary<string, double?> { ["s"] = 200 },
            });
    }

    [Fact, IsLayer0]
    public async Task CurrentStateWithOverlappingEvents()
    {
        RegisterResourceScaler("s", x => x ?? 10);
        RegisterResourceScaler("c", x => x ?? 10);
        var start = UtcNow;
        _cosmosDbPrescaleLeadTime = TimeSpan.FromMinutes(10);
        CreateScaleGroup();

        var events = new[]
        {
            (start: 15, end: 30),
            (start: 35, end: 50),
            (start: 36, end: 52),
            (start: 64, end: 80),
        };
        foreach (var e in events)
        {
            var scaleEvent = new Client.Models.ScaleEvent
            {
                Name = "aa",
                RegionConfig = new List<RegionScaleValue>
                {
                    new RegionScaleValue
                    {
                        Name = "eu",
                        Scale = 10,
                    }
                },
                RequiredScaleAt = start + TimeSpan.FromMinutes(e.start),
                StartScaleDownAt = start + TimeSpan.FromMinutes(e.end),
            };
            ApiClient.SaveScaleEvent("sg", Guid.NewGuid(), scaleEvent);
        }
        await AdvanceTimeTo(start + TimeSpan.FromMinutes(24));
        ScaleSetMonitorMoq.Setup(x => x.GetNumberOfWorkingInstances(GetLoadBalancerResourceId(), 9999))
            .ReturnsAsync(2);

        var state = ApiClient.GetCurrentState("sg");

        state.Should().NotBeNull();
        state.Regions.Should().HaveCount(1);
        state.Regions[0].Should().BeEquivalentTo(
            new ScaleRegionState
            {
                Name = "eu",
                WasScaledUpAt = start + TimeSpan.FromMinutes(5),
                WillScaleDownAt = start + TimeSpan.FromMinutes(52),
                Scale = 200,
                RequestedScale = 10,
                ScaleSetState = new Dictionary<string, double?> { ["s"] = 200 },
            });
    }

    [Fact, IsLayer0]
    public async Task CurrentStateAfterEventCompleted()
    {
        RegisterResourceScaler("s", x => x ?? 10);
        RegisterResourceScaler("c", x => x ?? 10);
        var start = UtcNow;
        _scaleSetPrescaleLeadTime = TimeSpan.FromMinutes(10);
        CreateScaleGroup();
        var scaleEvent = new Client.Models.ScaleEvent
        {
            Name = "aa",
            RegionConfig = new List<RegionScaleValue>
            {
                new RegionScaleValue
                {
                    Name = "eu",
                    Scale = 10,
                }
            },
            RequiredScaleAt = start + TimeSpan.FromMinutes(15),
            StartScaleDownAt = start + TimeSpan.FromHours(2),
        };
        ApiClient.SaveScaleEvent("sg", Guid.NewGuid(), scaleEvent);
        await AdvanceTimeTo(start + TimeSpan.FromHours(3));
        ScaleSetMonitorMoq.Setup(x => x.GetNumberOfWorkingInstances(GetLoadBalancerResourceId(), 9999))
            .ReturnsAsync(2);

        var state = ApiClient.GetCurrentState("sg");

        state.Should().NotBeNull();
        state.Regions.Should().BeEmpty();
    }

    private void CreateScaleGroup()
    {
        ApiClient.SetDefinition("sg", body: new Client.Models.ScaleGroupDefinition
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
                    ScaleSets = new List<ScaleSetConfiguration>
                    {
                        new ScaleSetConfiguration
                        {
                            Name = "s",
                            AutoscaleSettingsResourceId = GetAutoscaleSettingResourceId(),
                            ProfileName = "pr",
                            LoadBalancerResourceId = GetLoadBalancerResourceId(),
                            HealthPortPort = 9999,
                            RequestsPerInstance = 100,
                        },
                    },
                    CosmosDbPrescaleLeadTime = _cosmosDbPrescaleLeadTime.ToString(),
                    ScaleSetPrescaleLeadTime = _scaleSetPrescaleLeadTime.ToString(),
                },
                new ScaleGroupRegion
                {
                    RegionName = "eu1",
                    ScaleSets = new List<ScaleSetConfiguration>
                    {
                        new ScaleSetConfiguration
                        {
                            Name = "s",
                            AutoscaleSettingsResourceId = GetAutoscaleSettingResourceId(),
                            ProfileName = "pr",
                            LoadBalancerResourceId = GetLoadBalancerResourceId(),
                            HealthPortPort = 9999,
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
