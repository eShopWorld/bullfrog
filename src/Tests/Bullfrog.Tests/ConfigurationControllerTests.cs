﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Client;
using Client.Models;
using Eshopworld.Tests.Core;
using FluentAssertions;
using Xunit;

public class ConfigurationControllerTests : BaseApiTests
{
    [Fact, IsLayer0]
    public async Task EmptyListOfScaleGroups()
    {
        var scaleGroups = await ApiClient.ListScaleGroupsAsync();
        scaleGroups.Should().NotBeNull();
    }

    [Fact, IsLayer0]
    public async Task DeleteUnknownScaleGroupWhenNoneIsDefined()
    {
        await ApiClient.RemoveDefinitionAsync("unknown");
    }

    [Fact, IsLayer0]
    public async Task CreateNewScaleGroup()
    {
        //act
        await ApiClient.SetDefinitionAsync("sg", body: new ScaleGroupDefinition
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
                            Name = "aa",
                            MinimumRU = 400,
                            MaximumRU = 5000,
                            RequestUnitsPerRequest = 10,
                            DataPlaneConnection = new CosmosDbDataPlaneConnection
                            {
                                AccountName = "bb",
                                DatabaseName = "cc",
                            }
                        },
                    },
                    ScaleSets = new List<ScaleSetConfiguration>
                    {
                        new ScaleSetConfiguration
                        {
                            Name = "ss",
                            AutoscaleSettingsResourceId = GetAutoscaleSettingResourceId(),
                            ProfileName = "pr",
                            LoadBalancerResourceId = GetLoadBalancerResourceId(),
                            HealthPortPort = 9999,
                            RequestsPerInstance = 20,
                        },
                    },
                }
            },
        });

        var list = await ApiClient.ListScaleGroupsAsync();
        list.Should().BeEquivalentTo("sg");
    }

    [Fact, IsLayer0]
    public async Task MissingAutomationAccountsDefinitions()
    {
        var scaleGroupDefinition = GetScaleGroupWithRunbook();
        scaleGroupDefinition.AutomationAccounts = null;

        //act
        Func<Task> func = () => ApiClient.SetDefinitionAsync("sg", body: scaleGroupDefinition);

        func.Should().Throw<ProblemDetailsException>()
            .Where(x => x.Response.StatusCode == System.Net.HttpStatusCode.BadRequest);
    }

    [Fact, IsLayer0]
    public async Task ReferencedAccountNotDefined()
    {
        var scaleGroupDefinition = GetScaleGroupWithRunbook();
        scaleGroupDefinition.AutomationAccounts[0].Name = "bad";

        //act
        Func<Task> func = () => ApiClient.SetDefinitionAsync("sg", body: scaleGroupDefinition);

        func.Should().Throw<ProblemDetailsException>()
            .Where(x => x.Response.StatusCode == System.Net.HttpStatusCode.BadRequest);
    }

    [Fact, IsLayer0]
    public async Task DuplicatedNamesOfReferencedAccount()
    {
        var scaleGroupDefinition = GetScaleGroupWithRunbook();
        scaleGroupDefinition.AutomationAccounts.Add(new AutomationAccount
        {
            Name = scaleGroupDefinition.AutomationAccounts[0].Name,
            ResourceId = scaleGroupDefinition.AutomationAccounts[0].ResourceId + "aa",
        });

        //act
        Func<Task> func = () => ApiClient.SetDefinitionAsync("sg", body: scaleGroupDefinition);

        func.Should().Throw<ProblemDetailsException>()
            .Where(x => x.Response.StatusCode == System.Net.HttpStatusCode.BadRequest);
    }

    [Fact, IsLayer0]
    public async Task DuplicatedResourceIdsOfReferencedAccount()
    {
        var scaleGroupDefinition = GetScaleGroupWithRunbook();
        scaleGroupDefinition.AutomationAccounts.Add(new AutomationAccount
        {
            Name = "snd",
            ResourceId = scaleGroupDefinition.AutomationAccounts[0].ResourceId,
        });

        //act
        Func<Task> func = () => ApiClient.SetDefinitionAsync("sg", body: scaleGroupDefinition);

        func.Should().Throw<ProblemDetailsException>()
            .Where(x => x.Response.StatusCode == System.Net.HttpStatusCode.BadRequest);
    }

    private static ScaleGroupDefinition GetScaleGroupWithRunbook()
    {
        //act
        return new ScaleGroupDefinition
        {
            AutomationAccounts = new List<AutomationAccount>
            {
                new AutomationAccount("automationAccount1", "/subscriptions/00000000-1111-2222-3333-444444444444/resourceGroups/test/providers/Microsoft.Automation/automationAccounts/test2"),
            },
            Regions = new List<ScaleGroupRegion>
            {
                new ScaleGroupRegion
                {
                    RegionName = "eu",
                    Cosmos = new List<CosmosConfiguration>()
                    {
                    },
                    ScaleSets = new List<ScaleSetConfiguration>
                    {
                        new ScaleSetConfiguration
                        {
                            Name = "ss",
                            AutoscaleSettingsResourceId = GetAutoscaleSettingResourceId(),
                            ProfileName = "pr",
                            LoadBalancerResourceId = GetLoadBalancerResourceId(),
                            HealthPortPort = 9999,
                            RequestsPerInstance = 20,
                            Runbook = new ScaleSetRunbookConfiguration
                            {
                                AutomationAccountName = "automationAccount1",
                                RunbookName = "aa",
                                ScaleSetName = "d",
                            },
                        },
                    },
                }
            },
        };
    }

    [Fact, IsLayer0]
    public async Task UpdateScaleGroupWithoutChanges()
    {
        var scaleGroup = new ScaleGroupDefinition
        {
            Regions = new List<ScaleGroupRegion>
            {
                new ScaleGroupRegion
                {
                    RegionName = "eu",
                    Cosmos = new List<CosmosConfiguration>(),
                    ScaleSets = new List<ScaleSetConfiguration>(),
                }
            },
        };
        await ApiClient.SetDefinitionAsync("sg", body: scaleGroup);

        //act
        await ApiClient.SetDefinitionAsync("sg", body: scaleGroup);
    }

    [Fact, IsLayer0]
    public async Task FailIfNewDefinitionMissesUsedRegions()
    {
        var scaleGroup = new ScaleGroupDefinition
        {
            Regions = new List<ScaleGroupRegion>
            {
                new ScaleGroupRegion
                {
                    RegionName = "eu1",
                    Cosmos = new List<CosmosConfiguration>(),
                    ScaleSets = new List<ScaleSetConfiguration>(),
                },
                new ScaleGroupRegion
                {
                    RegionName = "eu2",
                    Cosmos = new List<CosmosConfiguration>(),
                    ScaleSets = new List<ScaleSetConfiguration>(),
                },
            },
        };
        await ApiClient.SetDefinitionAsync("sg", body: scaleGroup);
        await ApiClient.SaveScaleEventAsync("sg", Guid.NewGuid(), new ScaleEvent
        {
            Name = "n",
            RegionConfig = new[] { new RegionScaleValue { Name = "eu2", Scale = 10 } },
            RequiredScaleAt = UtcNow.AddHours(1),
            StartScaleDownAt = UtcNow.AddHours(2),
        });
        scaleGroup.Regions.RemoveAt(1); // try to remove eu2

        //act
        Func<Task> call = () => ApiClient.SetDefinitionAsync("sg", body: scaleGroup);

        call.Should().Throw<ProblemDetailsException>();
    }

    [Fact, IsLayer0]
    public async Task NotUsedRegionsMayBeRemoved()
    {
        var scaleGroup = new ScaleGroupDefinition
        {
            Regions = new List<ScaleGroupRegion>
            {
                new ScaleGroupRegion
                {
                    RegionName = "eu1",
                    Cosmos = new List<CosmosConfiguration>(),
                    ScaleSets = new List<ScaleSetConfiguration>(),
                },
                new ScaleGroupRegion
                {
                    RegionName = "eu2",
                    Cosmos = new List<CosmosConfiguration>(),
                    ScaleSets = new List<ScaleSetConfiguration>(),
                },
            },
        };
        await ApiClient.SetDefinitionAsync("sg", body: scaleGroup);
        await ApiClient.SaveScaleEventAsync("sg", Guid.NewGuid(), new ScaleEvent
        {
            Name = "n",
            RegionConfig = new[] { new RegionScaleValue { Name = "eu1", Scale = 10 } },
            RequiredScaleAt = UtcNow.AddHours(1),
            StartScaleDownAt = UtcNow.AddHours(2),
        });
        scaleGroup.Regions.RemoveAt(1); // try to remove eu2

        //act
        await ApiClient.SetDefinitionAsync("sg", body: scaleGroup);
    }

    [Fact, IsLayer0]
    public async Task RemoveExistingScaleGroup()
    {
        var scaleGroup = new ScaleGroupDefinition
        {
            Regions = new List<ScaleGroupRegion>
            {
                new ScaleGroupRegion
                {
                    RegionName = "eu",
                    Cosmos = new List<CosmosConfiguration>(),
                    ScaleSets = new List<ScaleSetConfiguration>(),
                }
            },
        };
        await ApiClient.SetDefinitionAsync("sg", body: scaleGroup);

        //act
        await ApiClient.RemoveDefinitionAsync("sg");

        Func<Task> func = () => ApiClient.GetDefinitionAsync("sg");
        func.Should().Throw<ProblemDetailsException>()
            .Where(x => x.Response.StatusCode == System.Net.HttpStatusCode.NotFound);
    }

    [Fact, IsLayer0]
    public async Task GettingUnknownScaleGroup()
    {
        var scaleGroup = new ScaleGroupDefinition
        {
            Regions = new List<ScaleGroupRegion>
            {
                new ScaleGroupRegion
                {
                    RegionName = "eu",
                    Cosmos = new List<CosmosConfiguration>(),
                    ScaleSets = new List<ScaleSetConfiguration>(),
                }
            },
        };
        await ApiClient.SetDefinitionAsync("sg", body: scaleGroup);

        //act
        Func<Task> func = () => ApiClient.GetDefinitionAsync("unkn");

        func.Should().Throw<ProblemDetailsException>()
            .Where(x => x.Response.StatusCode == System.Net.HttpStatusCode.NotFound);
    }

    [Fact, IsLayer0]
    public async Task GettingExistingScaleGroup()
    {
        var scaleGroup = new ScaleGroupDefinition
        {
            Regions = new List<ScaleGroupRegion>
            {
                new ScaleGroupRegion
                {
                    RegionName = "eu",
                    Cosmos = new List<CosmosConfiguration>(),
                    ScaleSets = new List<ScaleSetConfiguration>(),
                }
            },
        };
        await ApiClient.SetDefinitionAsync("sg", body: scaleGroup);

        //act
        var returnedScaleGroup = await ApiClient.GetDefinitionAsync("sg");

        var expectedScaleGroup = new ScaleGroupDefinition
        {
            Regions = new List<ScaleGroupRegion>
            {
                new ScaleGroupRegion
                {
                    RegionName = "eu",
                    Cosmos = new List<CosmosConfiguration>(),
                    ScaleSets = new List<ScaleSetConfiguration>(),
                    ScaleSetPrescaleLeadTime = TimeSpan.Zero.ToString(),
                    CosmosDbPrescaleLeadTime = TimeSpan.Zero.ToString(),
                }
            },
            CosmosDbPrescaleLeadTime = TimeSpan.Zero.ToString(),
        };
        returnedScaleGroup.Should().BeEquivalentTo(expectedScaleGroup);
    }
}
