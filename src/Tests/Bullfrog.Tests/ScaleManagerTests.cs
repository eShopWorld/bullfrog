using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Client;
using Client.Models;
using Eshopworld.Tests.Core;
using FluentAssertions;
using Xunit;

public class ScaleManagerTests : BaseApiTests
{
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
        var id = Guid.NewGuid();

        //act
        ApiClient.SaveScaleEvent("sg", id, scaleEvent);

        var scheduledEvent = ApiClient.GetScheduledEvent("sg", id);
        scheduledEvent.Should().BeEquivalentTo(scaleEvent);
    }



    private void CreateScaleGroup()
    {
        ApiClient.SetDefinition("sg", new Client.Models.ScaleGroupDefinition
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
                            RequestsPerRU = 2,
                        },
                    },
                    ScaleSets = new List<ScaleSetConfiguration>
                    {
                        new ScaleSetConfiguration
                        {
                            Name = "s",
                            AutoscaleSettingsResourceId = "ri",
                            ProfileName = "pr",
                            DefaultInstanceCount = 1,
                            MinInstanceCount = 1,
                            RequestsPerInstance = 30,
                        },
                    },
                }
            },
        });

    }
}
