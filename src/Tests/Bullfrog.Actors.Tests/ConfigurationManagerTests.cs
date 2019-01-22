using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
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
        AddMissingOptionalState<ScaleGroupDefinition>(ScaleGroupKeyPrefix + scaleGroupName);
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
        AddMissingOptionalState<ScaleGroupDefinition>(ScaleGroupKeyPrefix + scaleGroupName);
        ActorStateManagerMock.Setup(sm => sm.SetStateAsync(ScaleGroupKeyPrefix + scaleGroupName, newScaleGroupDefinition, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var configureCallVerify = PrepareScaleManagerConfigureCall("aa/rr", c => c.ScaleSetConfiguration == newScaleGroupDefinition.Regions[0].ScaleSet);
        var cm = GetActor();

        await cm.ConfigureScaleGroup(scaleGroupName, newScaleGroupDefinition, default);

        configureCallVerify();
        ActorStateManagerMock.Verify(sm => sm.SetStateAsync(ScaleGroupKeyPrefix + scaleGroupName, newScaleGroupDefinition, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact, IsDev]
    public async Task RemoveNotExistingScaleGroup()
    {
        var scaleGroupName = "aa";
        AddMissingOptionalState<ScaleGroupDefinition>(ScaleGroupKeyPrefix + scaleGroupName);
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
        AddOptionalState(ScaleGroupKeyPrefix + scaleGroupName, scaleGroupDefinition);
        ActorStateManagerMock.Setup(sm => sm.RemoveStateAsync(ScaleGroupKeyPrefix + scaleGroupName, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var disableVerify = PrepareScaleManagerDisableCall("aa/rr");
        var cm = GetActor();

        await cm.ConfigureScaleGroup(scaleGroupName, null, default);

        disableVerify();
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
        AddOptionalState(ScaleGroupKeyPrefix + scaleGroupName, existingScaleGroupDefinition);
        ActorStateManagerMock.Setup(sm => sm.SetStateAsync(ScaleGroupKeyPrefix + scaleGroupName, newScaleGroupDefinition, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        // region deleted
        var dlDisableVerify = PrepareScaleManagerDisableCall("aa/dl");
        // region changed
        var chConfigureVerify = PrepareScaleManagerConfigureCall("aa/ch", c => c.ScaleSetConfiguration == newScaleGroupDefinition.Regions[0].ScaleSet);
        // region added
        var nwConfigureVerify = PrepareScaleManagerConfigureCall("aa/nw", c => c.ScaleSetConfiguration == newScaleGroupDefinition.Regions[1].ScaleSet);
        var cm = GetActor();

        await cm.ConfigureScaleGroup(scaleGroupName, newScaleGroupDefinition, default);

        dlDisableVerify();
        chConfigureVerify();
        nwConfigureVerify();
        ActorStateManagerMock.Verify(sm => sm.SetStateAsync(ScaleGroupKeyPrefix + scaleGroupName, newScaleGroupDefinition, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private Action PrepareScaleManagerConfigureCall(string name, Expression<Func<ScaleManagerConfiguration, bool>> match)
    {
        var scaleManagerMock = MockRepository.Create<IScaleManager>();
        scaleManagerMock.Setup(sm => sm.Configure(It.Is(match), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        RegisterActorProxy(new ActorId("ScaleManager:" + name), scaleManagerMock.Object);
        return () => scaleManagerMock.Verify(sm => sm.Configure(It.IsAny<ScaleManagerConfiguration>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    private Action PrepareScaleManagerDisableCall(string name)
    {
        var scaleManagerMock = MockRepository.Create<IScaleManager>();
        scaleManagerMock.Setup(sm => sm.Disable(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        RegisterActorProxy(new ActorId("ScaleManager:" + name), scaleManagerMock.Object);
        return () => scaleManagerMock.Verify(sm => sm.Disable(It.IsAny<CancellationToken>()), Times.Once);
    }

    private new IConfigurationManager GetActor()
    {
        return base.GetActor();
    }
}
