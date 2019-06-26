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
        await ApiClient.SaveScaleEventAsync("sg", eventId, NewScaleEvent());

        var response = await ApiClient.SaveScaleEventWithHttpMessagesAsync("sg", eventId, NewScaleEvent());

        response.Response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        response.Response.Headers.Location.Should().BeNull();
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
    public async Task CreateEventInThePast()
    {
        CreateScaleGroup();
        var eventId = Guid.NewGuid();
        await AdvanceTimeTo(UtcNow.AddHours(2));

        Func<Task<HttpOperationResponse<ScheduledScaleEvent>>> call =
            () => ApiClient.SaveScaleEventWithHttpMessagesAsync("sg", eventId, NewScaleEvent(-2, -1));

        ShouldThrowBullfrogError(call, -1);
    }

    [Fact, IsLayer0]
    public async Task CreateEventWithInvalidRegion()
    {
        CreateScaleGroup();
        var eventId = Guid.NewGuid();
        await AdvanceTimeTo(UtcNow.AddHours(2));

        Func<Task<HttpOperationResponse<ScheduledScaleEvent>>> call =
            () => ApiClient.SaveScaleEventWithHttpMessagesAsync("sg", eventId, NewScaleEvent(-2, -1, new[] { ("r1", 30) }));

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
                }
            },
        };
    }
}
