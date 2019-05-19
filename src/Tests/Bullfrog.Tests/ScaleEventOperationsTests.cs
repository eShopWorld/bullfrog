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
using Newtonsoft.Json;
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
    }

    [Fact, IsLayer0]
    public async Task UpdateActiveEvent()
    {
        CreateScaleGroup();
        var eventId = Guid.NewGuid();
        await ApiClient.SaveScaleEventAsync("sg", eventId, NewScaleEvent(1, 3));
        var updatedEvent = NewScaleEvent(1, 4);
        await AdvanceTimeTo(UtcNow.AddHours(2));

        var response = await ApiClient.SaveScaleEventWithHttpMessagesAsync("sg", eventId, updatedEvent);

        response.Response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        response.Response.Headers.Location.Should().BeNull();
    }

    [Fact, IsLayer0]
    public async Task CreateEventInThePast()
    {
        CreateScaleGroup();
        var eventId = Guid.NewGuid();
        await AdvanceTimeTo(UtcNow.AddHours(2));

        Func<Task<HttpOperationResponse<ScheduledScaleEvent>>> call =
            () => ApiClient.SaveScaleEventWithHttpMessagesAsync("sg", eventId, NewScaleEvent(-2, -1));

        var response = call.Should().Throw<ProblemDetailsException>()
            .Which.Response;
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var json = JsonConvert.DeserializeObject<BullfrogErrorResponse>(response.Content);
        json.Should().BeEquivalentTo(new BullfrogErrorResponse
        {
            Errors = new[]
            {
                new BullfrogErrorDescription
                {
                    Code = "-1",
                }
            }
        }, o => o.Excluding(r => r.Errors[0].Message));
    }

    [Fact, IsLayer0]
    public async Task CreateEventWithInvalidRegion()
    {
        CreateScaleGroup();
        var eventId = Guid.NewGuid();
        await AdvanceTimeTo(UtcNow.AddHours(2));

        Func<Task<HttpOperationResponse<ScheduledScaleEvent>>> call =
            () => ApiClient.SaveScaleEventWithHttpMessagesAsync("sg", eventId, NewScaleEvent(-2, -1, new[] { ("r1", 30) }));

        var response = call.Should().Throw<ProblemDetailsException>()
            .Which.Response;
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var json = JsonConvert.DeserializeObject<BullfrogErrorResponse>(response.Content);
        json.Should().BeEquivalentTo(new BullfrogErrorResponse
        {
            Errors = new[]
            {
                new BullfrogErrorDescription
                {
                    Code = "-3",
                }
            }
        }, o => o.Excluding(r => r.Errors[0].Message));
    }

    [Fact, IsLayer0]
    public async Task DeleteNotStartedEvent()
    {
        CreateScaleGroup();
        var eventId = Guid.NewGuid();
        ApiClient.SaveScaleEvent("sg", eventId, NewScaleEvent());

        var msg = await ApiClient.DeleteScaleEventWithHttpMessagesAsync("sg", eventId);

        msg.Response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact, IsLayer0]
    public async Task DeleteAlreadyStartedEvent()
    {
        CreateScaleGroup();
        var eventId = Guid.NewGuid();
        ApiClient.SaveScaleEvent("sg", eventId, NewScaleEvent(1, 4));
        await AdvanceTimeTo(UtcNow.AddHours(2));

        var msg = await ApiClient.DeleteScaleEventWithHttpMessagesAsync("sg", eventId);

        msg.Response.StatusCode.Should().Be(HttpStatusCode.Accepted);
    }

    [Fact, IsLayer0]
    public async Task DeletePrescalingEvent()
    {
        var scaleGroupDefinition = NewScaleGroupDefinition();
        scaleGroupDefinition.Regions[0].ScaleSetPrescaleLeadTime = TimeSpan.FromHours(2).ToString();
        CreateScaleGroup(scaleGroupDefinition);
        var eventId = Guid.NewGuid();
        ApiClient.SaveScaleEvent("sg", eventId, NewScaleEvent(3, 8));
        await AdvanceTimeTo(UtcNow.AddHours(2));

        var msg = await ApiClient.DeleteScaleEventWithHttpMessagesAsync("sg", eventId);

        msg.Response.StatusCode.Should().Be(HttpStatusCode.Accepted);
    }

    [Fact, IsLayer0]
    public async Task DeleteCompletedEvent()
    {
        CreateScaleGroup();
        var eventId = Guid.NewGuid();
        ApiClient.SaveScaleEvent("sg", eventId, NewScaleEvent(1, 4));
        await AdvanceTimeTo(UtcNow.AddHours(5));

        var msg = await ApiClient.DeleteScaleEventWithHttpMessagesAsync("sg", eventId);

        msg.Response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact, IsLayer0]
    public async Task DeleteUnknownEvent()
    {
        CreateScaleGroup();
        ApiClient.SaveScaleEvent("sg", Guid.NewGuid(), NewScaleEvent(1, 4));
        await AdvanceTimeTo(UtcNow.AddHours(2));

        Func<Task> func = () => ApiClient.DeleteScaleEventWithHttpMessagesAsync("sg", Guid.NewGuid());

        func.Should().Throw< ProblemDetailsException> ()
            .Which.Response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }


    [Fact, IsLayer0]
    public async Task DeleteEventFromUnknownGroup()
    {
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
                            AutoscaleSettingsResourceId = "/subscriptions/00000000-0000-0000-0000-000000000001/ri",
                            ProfileName = "pr",
                            LoadBalancerResourceId = "/subscriptions/00000000-0000-0000-0000-000000000001/lb",
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


public class BullfrogErrorResponse
{
    public IList<BullfrogErrorDescription> Errors { get; set; }
}

public class BullfrogErrorDescription
{
    public string Code { get; set; }

    public string Message { get; set; }
}
