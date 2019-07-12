using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Client;
using Client.Models;
using Eshopworld.Tests.Core;
using FluentAssertions;
using Xunit;

public class SharedCosmosDbTests : BaseApiTests
{
    [Fact, IsLayer0]
    public void CreateScaleGroupWithSharedCosmosDb()
    {
        CreateScaleGroup();

        var createdScaleGroup = ApiClient.GetDefinition("sg");

        createdScaleGroup.Should().BeEquivalentTo(NewScaleGroupDefinition());
    }

    [Fact, IsLayer0]
    public void SharedCosmosLeadTimeShouldBeTakenIntoAccount()
    {
        var scaleGroup = NewScaleGroupDefinition();
        scaleGroup.Regions[0].ScaleSetPrescaleLeadTime = TimeSpan.FromMinutes(10).ToString();
        CreateScaleGroup(scaleGroup);

        var created = ApiClient.SaveScaleEvent("sg", Guid.NewGuid(), NewScaleEvent(10, 20, regions: new[] { ("eu1", 10), }));

        created.EstimatedScaleUpAt.Should().Be(UtcNow.AddHours(10).AddMinutes(-30));
        created.RegionConfig.Should().HaveCount(1);
        created.RegionConfig[0].Name.Should().Be("eu1");
    }

    [Fact, IsLayer0]
    public async Task SharedCosmosDbIsScaledOut()
    {
        CreateScaleGroup();
        ApiClient.SaveScaleEvent("sg", Guid.NewGuid(), NewScaleEvent());

        await AdvanceTimeTo(UtcNow.AddHours(15));

        ScaleHistory["c1"].Last().RequestedThroughput.Should().Be(30);
    }

    [Fact, IsLayer0]
    public async Task SharedCosmosDbIsNotScaledOutIfTheEventHasBeenDeletedEarlier()
    {
        CreateScaleGroup();
        var eventId = Guid.NewGuid();
        ApiClient.SaveScaleEvent("sg", eventId, NewScaleEvent());
        ApiClient.DeleteScaleEvent("sg", eventId);

        await AdvanceTimeTo(UtcNow.AddHours(15));

        ScaleHistory["c1"].Should().BeEmpty();
    }

    [Fact, IsLayer0]
    public async Task SharedCosmosDbIsNotScaledOutIfSharedDatabaseIsRemovedEarlier()
    {
        CreateScaleGroup();
        var eventId = Guid.NewGuid();
        ApiClient.SaveScaleEvent("sg", eventId, NewScaleEvent());
        var updatedConfiguration = NewScaleGroupDefinition();
        updatedConfiguration.Cosmos = null;
        ApiClient.SetDefinition("sg", updatedConfiguration);

        await AdvanceTimeTo(UtcNow.AddHours(15));

        ScaleHistory["c1"].Should().BeEmpty();
    }


    [Fact, IsLayer0]
    public async Task SharedCosmosDbIsNotScaledOutIfScaleGrupIsRemoved()
    {
        CreateScaleGroup();
        var eventId = Guid.NewGuid();
        ApiClient.SaveScaleEvent("sg", eventId, NewScaleEvent());
        ApiClient.RemoveDefinition("sg");

        await AdvanceTimeTo(UtcNow.AddHours(15));

        ScaleHistory["c1"].Should().BeEmpty();
    }

    private ScaleEvent NewScaleEvent(int scaleOut = 10, int scaleIn = 20, IEnumerable<(string regionName, int scale)> regions = null)
    {
        if (regions == null)
            regions = new[] { ("eu1", 10), ("eu2", 20), ("eu3", 30) };
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
        if (scaleGroupDefinition == null)
            scaleGroupDefinition = NewScaleGroupDefinition();
        ApiClient.SetDefinition("sg", scaleGroupDefinition);
        var allResources = scaleGroupDefinition.Cosmos.Select(x => x.Name)
            .Union(scaleGroupDefinition.Regions.SelectMany(r => r.ScaleSets).Select(x => x.Name));
        foreach (var resources in allResources)
        {
            RegisterResourceScaler(resources, x => x ?? 10);
        }
    }

    private static ScaleGroupDefinition NewScaleGroupDefinition() => new ScaleGroupDefinition
    {
        Cosmos = new List<CosmosConfiguration>
        {
            new CosmosConfiguration
            {
                Name = "c1",
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
        CosmosDbPrescaleLeadTime = TimeSpan.FromMinutes(30).ToString(),
        Regions = new List<ScaleGroupRegion>
            {
                new ScaleGroupRegion
                {
                    RegionName = "eu1",
                    ScaleSets = new List<ScaleSetConfiguration>
                    {
                        new ScaleSetConfiguration
                        {
                            Name = "s1",
                            AutoscaleSettingsResourceId = GetAutoscaleSettingResourceId(),
                            ProfileName = "pr",
                            LoadBalancerResourceId = GetLoadBalancerResourceId(),
                            HealthPortPort = 9999,
                            RequestsPerInstance = 100,
                            ReservedInstances = 0,
                        },
                    },
                    CosmosDbPrescaleLeadTime = TimeSpan.Zero.ToString(),
                    ScaleSetPrescaleLeadTime = TimeSpan.FromHours(1).ToString(),
                },
                new ScaleGroupRegion
                {
                    RegionName = "eu2",
                    ScaleSets = new List<ScaleSetConfiguration>
                    {
                        new ScaleSetConfiguration
                        {
                            Name = "s2",
                            AutoscaleSettingsResourceId = GetAutoscaleSettingResourceId(),
                            ProfileName = "pr",
                            LoadBalancerResourceId = GetLoadBalancerResourceId(),
                            HealthPortPort = 9999,
                            RequestsPerInstance = 100,
                            ReservedInstances = 0,
                        },
                    },
                    CosmosDbPrescaleLeadTime = TimeSpan.Zero.ToString(),
                    ScaleSetPrescaleLeadTime = TimeSpan.FromHours(2).ToString(),
                },
                new ScaleGroupRegion
                {
                    RegionName = "eu3",
                    ScaleSets = new List<ScaleSetConfiguration>
                    {
                        new ScaleSetConfiguration
                        {
                            Name = "s3",
                            AutoscaleSettingsResourceId = GetAutoscaleSettingResourceId(),
                            ProfileName = "pr",
                            LoadBalancerResourceId = GetLoadBalancerResourceId(),
                            HealthPortPort = 9999,
                            RequestsPerInstance = 100,
                            ReservedInstances = 0,
                        },
                    },
                    CosmosDbPrescaleLeadTime = TimeSpan.Zero.ToString(),
                    ScaleSetPrescaleLeadTime = TimeSpan.FromHours(3).ToString(),
                }
           },
    };
}
