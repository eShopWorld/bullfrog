using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bullfrog.Actors;
using Bullfrog.Actors.Interfaces;
using Bullfrog.Actors.Interfaces.Models;
using Eshopworld.Tests.Core;
using FluentAssertions;
using Microsoft.ServiceFabric.Actors;
using Microsoft.ServiceFabric.Data;
using Moq;
using Xunit;

public class ConfigurationManagerTests : ActorTestsBase<ConfigurationManager>
{
    private const string ScaleGroupKeyPrefix = "scaleGroup:";

    [Fact, IsDev]
    public async Task ListScaleGroupsWhenNoneExist()
    {
        ActorStateManagerMock.Setup(sm => sm.GetStateNamesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(Enumerable.Empty<string>());
        var cm = GetActor();

        var scaleGroups = await cm.ListConfiguredScaleGroup(default);

        scaleGroups.Should().BeEmpty();
    }

    [Fact, IsDev]
    public async Task ListExistingScaleGrups()
    {
        ActorStateManagerMock.Setup(sm => sm.GetStateNamesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { "aa", ScaleGroupKeyPrefix + "bb", "cc", ScaleGroupKeyPrefix + "dd" });
        var cm = GetActor();

        var scaleGroups = await cm.ListConfiguredScaleGroup(default);

        scaleGroups.Should().BeEquivalentTo("bb", "dd");
    }

    [Fact, IsDev]
    public async Task GetExistingScaleGroupDefinition()
    {
        var scaleGroupName = "aa";
        var scaleGroupDefinition = new ScaleGroupDefinition
        {
            Regions = new List<ScaleGroupRegion>
            {
                new ScaleGroupRegion
                {
                    RegionName = "rr",
                    ScaleSet = new ScaleSetConfiguration
                    {
                    }
                }
            }
        };
        ActorStateManagerMock.Setup(sm => sm.TryGetStateAsync<ScaleGroupDefinition>(ScaleGroupKeyPrefix + scaleGroupName, It.IsAny<CancellationToken>()))
           .ReturnsAsync(new ConditionalValue<ScaleGroupDefinition>(true, scaleGroupDefinition));
        var cm = GetActor();

        var scaleGroup = await cm.GetScaleGroupConfiguration(scaleGroupName, default);

        scaleGroup.Should().Be(scaleGroupDefinition);
    }

    [Fact, IsDev]
    public async Task GetDefinitionOfNotDefinedScaleGroup()
    {
        var scaleGroupName = "aa";
        ActorStateManagerMock.Setup(sm => sm.TryGetStateAsync<ScaleGroupDefinition>(ScaleGroupKeyPrefix + scaleGroupName, It.IsAny<CancellationToken>()))
           .ReturnsAsync(new ConditionalValue<ScaleGroupDefinition>());
        var cm = GetActor();

        var scaleGroup = await cm.GetScaleGroupConfiguration(scaleGroupName, default);

        scaleGroup.Should().BeNull();
    }

    [Fact, IsDev]
    public async Task ConfigureNewScaleGroup()
    {
        var scaleGroupName = "aa";
        var newScaleGroupDefinition = new ScaleGroupDefinition
        {
            Regions = new List<ScaleGroupRegion>
            {
                new ScaleGroupRegion
                {
                    RegionName = "rr",
                    ScaleSet = new ScaleSetConfiguration
                    {
                        AutoscaleSettingsResourceId = "/xx",
                        DefaultInstanceCount = 2,
                        MinInstanceCount = 1,
                        ProfileName = "a",
                        RequestsPerInstance = 22,
                    }
                }
            }
        };
        ActorStateManagerMock.Setup(sm => sm.TryGetStateAsync<ScaleGroupDefinition>(ScaleGroupKeyPrefix + scaleGroupName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConditionalValue<ScaleGroupDefinition>());
        ActorStateManagerMock.Setup(sm => sm.SetStateAsync(ScaleGroupKeyPrefix + scaleGroupName, newScaleGroupDefinition, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var scaleManagerMock = MockRepository.Create<IScaleManager>();
        scaleManagerMock.Setup(sm => sm.Configure(It.Is<ScaleManagerConfiguration>(c => c.ScaleSetConfiguration == newScaleGroupDefinition.Regions[0].ScaleSet), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        RegisterActorProxy(new ActorId("ScaleManager:aa/rr"), scaleManagerMock.Object);
        var cm = GetActor();

        await cm.ConfigureScaleGroup(scaleGroupName, newScaleGroupDefinition, default);

        ActorStateManagerMock.Verify(sm => sm.SetStateAsync(ScaleGroupKeyPrefix + scaleGroupName, newScaleGroupDefinition, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact, IsDev]
    public async Task RemoveNotExistingScaleGroup()
    {
        var scaleGroupName = "aa";
        ActorStateManagerMock.Setup(sm => sm.TryGetStateAsync<ScaleGroupDefinition>(ScaleGroupKeyPrefix + scaleGroupName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConditionalValue<ScaleGroupDefinition>());
        var cm = GetActor();

        await cm.ConfigureScaleGroup(scaleGroupName, null, default);
    }

    [Fact, IsDev]
    public async Task RemoveExistingScaleGroup()
    {
        var scaleGroupName = "aa";
        var scaleGroupDefinition = new ScaleGroupDefinition
        {
            Regions = new List<ScaleGroupRegion>
            {
                new ScaleGroupRegion
                {
                    RegionName = "rr",
                    ScaleSet = new ScaleSetConfiguration
                    {
                        AutoscaleSettingsResourceId = "/xx",
                        DefaultInstanceCount = 2,
                        MinInstanceCount = 1,
                        ProfileName = "a",
                        RequestsPerInstance = 22,
                    }
                }
            }
        };
        ActorStateManagerMock.Setup(sm => sm.TryGetStateAsync<ScaleGroupDefinition>(ScaleGroupKeyPrefix + scaleGroupName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConditionalValue<ScaleGroupDefinition>(true, scaleGroupDefinition));
        ActorStateManagerMock.Setup(sm => sm.RemoveStateAsync(ScaleGroupKeyPrefix + scaleGroupName, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var scaleManagerMock = MockRepository.Create<IScaleManager>();
        scaleManagerMock.Setup(sm => sm.Disable(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        RegisterActorProxy(new ActorId("ScaleManager:aa/rr"), scaleManagerMock.Object);
        var cm = GetActor();

        await cm.ConfigureScaleGroup(scaleGroupName, null, default);

        scaleManagerMock.Verify(sm => sm.Disable(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact, IsDev]
    public async Task ModifyRegionsOfExistingScaleGroup()
    {
        var scaleGroupName = "aa";
        var existingScaleGroupDefinition = new ScaleGroupDefinition
        {
            Regions = new List<ScaleGroupRegion>
            {
                new ScaleGroupRegion
                {
                    RegionName = "dl",
                    ScaleSet = new ScaleSetConfiguration()
                },
                new ScaleGroupRegion
                {
                    RegionName = "ch",
                    ScaleSet = new ScaleSetConfiguration()
                }
            }
        };
        var newScaleGroupDefinition = new ScaleGroupDefinition
        {
            Regions = new List<ScaleGroupRegion>
            {
                new ScaleGroupRegion
                {
                    RegionName = "ch",
                    ScaleSet = new ScaleSetConfiguration()
                },
                new ScaleGroupRegion
                {
                    RegionName = "nw",
                    ScaleSet = new ScaleSetConfiguration()
                }
            }
        };
        ActorStateManagerMock.Setup(sm => sm.TryGetStateAsync<ScaleGroupDefinition>(ScaleGroupKeyPrefix + scaleGroupName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConditionalValue<ScaleGroupDefinition>(true, existingScaleGroupDefinition));
        ActorStateManagerMock.Setup(sm => sm.SetStateAsync(ScaleGroupKeyPrefix + scaleGroupName, newScaleGroupDefinition, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        // region deleted
        var dlScaleManagerMock = MockRepository.Create<IScaleManager>();
        dlScaleManagerMock.Setup(sm => sm.Disable(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        RegisterActorProxy(new ActorId("ScaleManager:aa/dl"), dlScaleManagerMock.Object);
        // region changed
        var chScaleManagerMock = MockRepository.Create<IScaleManager>();
        chScaleManagerMock.Setup(sm => sm.Configure(It.Is<ScaleManagerConfiguration>(c => c.ScaleSetConfiguration == newScaleGroupDefinition.Regions[0].ScaleSet), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        RegisterActorProxy(new ActorId("ScaleManager:aa/ch"), chScaleManagerMock.Object);
        // region added
        var nwScaleManagerMock = MockRepository.Create<IScaleManager>();
        nwScaleManagerMock.Setup(sm => sm.Configure(It.Is<ScaleManagerConfiguration>(c => c.ScaleSetConfiguration == newScaleGroupDefinition.Regions[1].ScaleSet), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        RegisterActorProxy(new ActorId("ScaleManager:aa/nw"), nwScaleManagerMock.Object);
        var cm = GetActor();

        await cm.ConfigureScaleGroup(scaleGroupName, newScaleGroupDefinition, default);

        dlScaleManagerMock.Verify(sm => sm.Disable(It.IsAny<CancellationToken>()), Times.Once);
        chScaleManagerMock.Verify(sm => sm.Configure(It.IsAny<ScaleManagerConfiguration>(), It.IsAny<CancellationToken>()), Times.Once);
        nwScaleManagerMock.Verify(sm => sm.Configure(It.IsAny<ScaleManagerConfiguration>(), It.IsAny<CancellationToken>()), Times.Once);
        ActorStateManagerMock.Verify(sm => sm.SetStateAsync(ScaleGroupKeyPrefix + scaleGroupName, newScaleGroupDefinition, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private new IConfigurationManager GetActor()
    {
        return base.GetActor();
    }
}
