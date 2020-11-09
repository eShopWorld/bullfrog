using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Client;
using Client.Models;
using Eshopworld.Tests.Core;
using FluentAssertions;
using Microsoft.Rest;
using Xunit;

public class ScaleEventOperationsTests : BaseApiTests
{
    [Fact, IsLayer0]
    public async Task CreatingNewScaleEvent()
    {
        CreateScaleGroup();

        var response = await ApiClient.SaveScaleEventWithHttpMessagesAsync("sg", Guid.NewGuid(), NewScaleEvent());

        response.Response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Response.Headers.Location.Should().NotBeNull();
        response.Dispose();
    }

    [Fact, IsLayer0]
    public async Task UpdateNotActiveEvent()
    {
        CreateScaleGroup();
        var eventId = Guid.NewGuid();
        await ApiClient.SaveScaleEventAsync("sg", eventId, NewScaleEvent(regions: new[] {("eu", 50)}));

        var response = await ApiClient.SaveScaleEventWithHttpMessagesAsync("sg", eventId, NewScaleEvent(regions: new[] { ("eu", 60) }));

        response.Response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        response.Response.Headers.Location.Should().BeNull();

        var updatedEvent = await ApiClient.GetScheduledEventAsync("sg", eventId);
        updatedEvent.Should().NotBeNull();
        updatedEvent.RegionConfig.Should().HaveCount(1);
        updatedEvent.RegionConfig[0].Scale.Should().Be(60);

        response.Dispose();
    }

    [Fact, IsLayer0]
    public async Task UpdateActiveEvent()
    {
        RegisterDefaultScalers();
        CreateScaleGroup();
        var eventId = Guid.NewGuid();
        await ApiClient.SaveScaleEventAsync("sg", eventId, NewScaleEvent(1, 3));
        var updatedEvent = NewScaleEvent(1, 4);
        await AdvanceTimeTo(UtcNow.AddHours(2));

        var response = await ApiClient.SaveScaleEventWithHttpMessagesAsync("sg", eventId, updatedEvent);

        response.Response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        response.Response.Headers.Location.Should().BeNull();
        response.Dispose();
    }

    [Fact, IsLayer0]
    public async Task UpdateAlreadyFinishedEvent()
    {
        RegisterDefaultScalers();
        CreateScaleGroup();
        var eventId = Guid.NewGuid();
        await ApiClient.SaveScaleEventAsync("sg", eventId, NewScaleEvent(1, 3));
        await AdvanceTimeTo(UtcNow.AddHours(6));

        Task<HttpOperationResponse<ScheduledScaleEvent>> call()
            => ApiClient.SaveScaleEventWithHttpMessagesAsync("sg", eventId, NewScaleEvent(8, 9));

        ShouldThrowBullfrogError(call, -1);
    }

    [Fact, IsLayer0]
    public async Task CreateEventInThePast()
    {
        CreateScaleGroup();
        var eventId = Guid.NewGuid();
        await AdvanceTimeTo(UtcNow.AddHours(2));

        Task<HttpOperationResponse<ScheduledScaleEvent>> call()
            => ApiClient.SaveScaleEventWithHttpMessagesAsync("sg", eventId, NewScaleEvent(-2, -1));

        ShouldThrowBullfrogError(call, -1);
    }

    [Fact, IsLayer0]
    public async Task CreateEventWithInvalidRegion()
    {
        CreateScaleGroup();
        var eventId = Guid.NewGuid();
        await AdvanceTimeTo(UtcNow.AddHours(2));

        Task<HttpOperationResponse<ScheduledScaleEvent>> call()
            => ApiClient.SaveScaleEventWithHttpMessagesAsync("sg", eventId, NewScaleEvent(-2, -1, new[] { ("r1", 30) }));

        ShouldThrowBullfrogError(call, -3);
    }

    [Fact, IsLayer0]
    public async Task DeleteNotStartedEvent()
    {
        CreateScaleGroup();
        var eventId = Guid.NewGuid();
        ApiClient.SaveScaleEvent("sg", eventId, NewScaleEvent());

        var msg = await ApiClient.DeleteScaleEventWithHttpMessagesAsync("sg", eventId);

        msg.Response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        msg.Dispose();
    }

    [Fact, IsLayer0]
    public async Task DeleteAlreadyStartedEvent()
    {
        RegisterDefaultScalers();
        CreateScaleGroup();
        var eventId = Guid.NewGuid();
        ApiClient.SaveScaleEvent("sg", eventId, NewScaleEvent(1, 4));
        await AdvanceTimeTo(UtcNow.AddHours(2));

        var msg = await ApiClient.DeleteScaleEventWithHttpMessagesAsync("sg", eventId);

        msg.Response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        msg.Dispose();
    }

    [Fact, IsLayer0]
    public async Task DeletePrescalingEvent()
    {
        RegisterDefaultScalers();
        var scaleGroupDefinition = NewScaleGroupDefinition();
        scaleGroupDefinition.Regions[0].ScaleSetPrescaleLeadTime = TimeSpan.FromHours(2).ToString();
        CreateScaleGroup(scaleGroupDefinition);
        var eventId = Guid.NewGuid();
        ApiClient.SaveScaleEvent("sg", eventId, NewScaleEvent(3, 8));
        await AdvanceTimeTo(UtcNow.AddHours(2));

        var msg = await ApiClient.DeleteScaleEventWithHttpMessagesAsync("sg", eventId);

        msg.Response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        msg.Dispose();
    }

    [Fact, IsLayer0]
    public async Task DeleteCompletedEvent()
    {
        RegisterDefaultScalers();
        CreateScaleGroup();
        var eventId = Guid.NewGuid();
        ApiClient.SaveScaleEvent("sg", eventId, NewScaleEvent(1, 4));
        await AdvanceTimeTo(UtcNow.AddHours(5));

        var msg = await ApiClient.DeleteScaleEventWithHttpMessagesAsync("sg", eventId);

        msg.Response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        msg.Dispose();
    }

    [Fact, IsLayer0]
    public async Task DeleteUnknownEvent()
    {
        RegisterDefaultScalers();
        CreateScaleGroup();
        ApiClient.SaveScaleEvent("sg", Guid.NewGuid(), NewScaleEvent(1, 4));
        await AdvanceTimeTo(UtcNow.AddHours(2));

        Func<Task> func = () => ApiClient.DeleteScaleEventWithHttpMessagesAsync("sg", Guid.NewGuid());

        func.Should().Throw<ProblemDetailsException>()
            .Which.Response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact, IsLayer0]
    public async Task DeleteEventFromUnknownGroup()
    {
        RegisterDefaultScalers();
        CreateScaleGroup();
        var eventId = Guid.NewGuid();
        ApiClient.SaveScaleEvent("sg", eventId, NewScaleEvent(1, 4));
        await AdvanceTimeTo(UtcNow.AddHours(2));

        Func<Task> func = () => ApiClient.DeleteScaleEventWithHttpMessagesAsync("sg1", eventId);

        func.Should().Throw<ProblemDetailsException>()
            .Which.Response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact, IsLayer0]
    public async Task OldEventsArePurged()
    {
        RegisterDefaultScalers();
        var scaleGroup = NewScaleGroupDefinition();
        scaleGroup.OldEventsAge = TimeSpan.FromDays(1).ToString();
        CreateScaleGroup(scaleGroup);
        var eventId1 = SaveNewEvent("sg", NewScaleEvent(1, 4)).Id.Value;
        var eventId2 = SaveNewEvent("sg", NewScaleEvent(10, 23)).Id.Value;
        var eventId3 = SaveNewEvent("sg", NewScaleEvent(10, 24)).Id.Value;
        var eventId4 = SaveNewEvent("sg", NewScaleEvent(30, 48)).Id.Value;
        var start = UtcNow;

        await ValidateStateAt(start.AddHours(24), new[] { eventId1, eventId2, eventId3, eventId4 }, null);
        await ValidateStateAt(start.AddHours(29), new[] { eventId2, eventId3, eventId4 }, new[] { eventId1, });
        await ValidateStateAt(start.AddHours(48), new[] { eventId3, eventId4 }, new[] { eventId1, eventId2, });
        await ValidateStateAt(start.AddHours(49), new[] { eventId4 }, new[] { eventId1, eventId2, eventId3, });
        await ValidateStateAt(start.AddHours(73), null, new[] { eventId1, eventId2, eventId3, eventId4, });
    }

    private async Task ValidateStateAt(DateTimeOffset time, Guid[] expectedEvents, Guid[] notExpectedEvents)
    {
        await AdvanceTimeTo(time);
        SaveNewEvent("sg", NewScaleEvent(30, 31)); // triggers purge operation
        var existing = ApiClient.ListScheduledEvents("sg");
        if (expectedEvents != null)
            existing.Select(x => x.Id).Should().Contain(expectedEvents);
        if (notExpectedEvents != null)
            existing.Select(x => x.Id).Should().NotContain(notExpectedEvents);
    }

    private ScheduledScaleEvent SaveNewEvent(string scaleGroup, ScaleEvent scaleEvent)
    {
        return ApiClient.SaveScaleEvent(scaleGroup, Guid.NewGuid(), scaleEvent);
    }

    private ScaleEvent NewScaleEvent(int scaleOut = 10, int scaleIn = 20, IEnumerable<(string regionName, int scale)> regions = null)
    {
        if (regions == null)
            regions = new[] { ("eu", 10) };
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
        ApiClient.SetDefinition("sg", body: scaleGroupDefinition ?? NewScaleGroupDefinition());
    }

    private static ScaleGroupDefinition NewScaleGroupDefinition()
    {
        return new ScaleGroupDefinition
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
                }
            },
        };
    }
}
