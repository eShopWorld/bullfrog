using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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
    public void DeletingEvent()
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
    public void AddingActiveEvent()
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
        var id = Guid.NewGuid();
        CosmosManagerMoq.Setup(x => x.SetScale(10, It.IsAny<Bullfrog.Actors.Interfaces.Models.CosmosConfiguration>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(4);
        ScaleSetManagerMoq.Setup(x => x.SetScale(10, It.IsAny<Bullfrog.Actors.Interfaces.Models.ScaleSetConfiguration>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(4);

        //act
        ApiClient.SaveScaleEvent("sg", id, scaleEvent);

        CosmosManagerMoq.Verify(x => x.SetScale(10, It.IsAny<Bullfrog.Actors.Interfaces.Models.CosmosConfiguration>(), It.IsAny<CancellationToken>()));
        ScaleSetManagerMoq.Verify(x => x.SetScale(10, It.IsAny<Bullfrog.Actors.Interfaces.Models.ScaleSetConfiguration>(), It.IsAny<CancellationToken>()));
    }

    [Theory, IsLayer0]
    [InlineData(4, 5, 60, null, null, 55)]
    [InlineData(5, 5, -30, 100, 100, 30)]
    [InlineData(5, 5, -60, null, null, null)]
    [InlineData(10, 6, 60, null, null, 50)]
    [InlineData(20, 20, 10, 100, 100, 70)]
    [InlineData(20, 8, 10, 100, null, 2)]
    [InlineData(9, 20, 10, null, 100, 1)]
    public void ReminderChecks(int scaleSetLeadTime, int cosmosDbLeadTime, int eventOffset, int? scaleSetScale, int? cosmosScale, int? reminder)
    {
        _scaleSetPrescaleLeadTime = TimeSpan.FromMinutes(scaleSetLeadTime);
        _cosmosDbPrescaleLeadTime = TimeSpan.FromMinutes(cosmosDbLeadTime);
        CreateScaleGroup();
        var scaleEvent = new Client.Models.ScaleEvent
        {
            Name = "aa",
            RegionConfig = new List<RegionScaleValue>
            {
                new RegionScaleValue
                {
                    Name = "eu",
                    Scale = 100,
                }
            },
            RequiredScaleAt = UtcNow + TimeSpan.FromMinutes(eventOffset),
            StartScaleDownAt = UtcNow + TimeSpan.FromMinutes(eventOffset + 60),
        };
        var id = Guid.NewGuid();
        if (cosmosScale.HasValue)
            CosmosManagerMoq.Setup(x => x.SetScale(cosmosScale.Value, It.IsAny<Bullfrog.Actors.Interfaces.Models.CosmosConfiguration>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(10);
        else
            CosmosManagerMoq.Setup(x => x.Reset(It.IsAny<Bullfrog.Actors.Interfaces.Models.CosmosConfiguration>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(10);
        if (scaleSetScale.HasValue)
            ScaleSetManagerMoq.Setup(x => x.SetScale(scaleSetScale.Value, It.IsAny<Bullfrog.Actors.Interfaces.Models.ScaleSetConfiguration>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(10);
        else
            ScaleSetManagerMoq.Setup(x => x.Reset(It.IsAny<Bullfrog.Actors.Interfaces.Models.ScaleSetConfiguration>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(10);

        //act
        ApiClient.SaveScaleEvent("sg", id, scaleEvent);

        CosmosManagerMoq.VerifyAll();
        ScaleSetManagerMoq.VerifyAll();
        var reminders = ScaleManagerActors[("sg", "eu")].GetActorReminders();
        if (reminder.HasValue)
            reminders.Should().ContainSingle()
                .Which.Should().Match<Microsoft.ServiceFabric.Actors.Runtime.IActorReminder>(x => x.DueTime == TimeSpan.FromMinutes(reminder.Value));
        else
            reminders.Should().BeEmpty();
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
