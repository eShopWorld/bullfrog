using System;
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
        await ApiClient.SetDefinitionAsync("sg", new Client.Models.ScaleGroupDefinition
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
                            AccountName = "bb",
                            DatabaseName = "cc",
                            MinimumRU = 400,
                            MaximumRU = 5000,
                            RequestsPerRU = 10,
                        },
                    },
                    ScaleSets = new List<ScaleSetConfiguration>
                    {
                        new ScaleSetConfiguration
                        {
                            Name = "aa",
                             AutoscaleSettingsResourceId = "ri",
                            ProfileName = "pr",
                            MinInstanceCount = 1,
                            DefaultInstanceCount = 1,
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
    public async Task UpdateScaleGroupWithoutChanges()
    {
        var scaleGroup = new Client.Models.ScaleGroupDefinition
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
        await ApiClient.SetDefinitionAsync("sg", scaleGroup);

        //act
        await ApiClient.SetDefinitionAsync("sg", scaleGroup);
    }


    [Fact, IsLayer0]
    public async Task RemoveExistingScaleGroup()
    {
        var scaleGroup = new Client.Models.ScaleGroupDefinition
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
        await ApiClient.SetDefinitionAsync("sg", scaleGroup);

        //act
        await ApiClient.RemoveDefinitionAsync("sg");

        Func<Task> func = () => ApiClient.GetDefinitionAsync("sg");
        func.Should().Throw<ProblemDetailsException>()
            .Where(x => x.Response.StatusCode == System.Net.HttpStatusCode.NotFound);
    }

    [Fact, IsLayer0]
    public async Task GettingUnknownScaleGroup()
    {
        var scaleGroup = new Client.Models.ScaleGroupDefinition
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
        await ApiClient.SetDefinitionAsync("sg", scaleGroup);

        //act
        Func<Task> func = () => ApiClient.GetDefinitionAsync("unkn");

        func.Should().Throw<ProblemDetailsException>()
            .Where(x => x.Response.StatusCode == System.Net.HttpStatusCode.NotFound);
    }

    [Fact, IsLayer0]
    public async Task GettingExistingScaleGroup()
    {
        var scaleGroup = new Client.Models.ScaleGroupDefinition
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
        await ApiClient.SetDefinitionAsync("sg", scaleGroup);

        //act
        var returnedScaleGroup = await ApiClient.GetDefinitionAsync("sg");

        var expectedScaleGroup = new Client.Models.ScaleGroupDefinition
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
        };
        returnedScaleGroup.Should().BeEquivalentTo(expectedScaleGroup);
    }
}
