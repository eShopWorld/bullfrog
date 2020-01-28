using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Client;
using Client.Models;
using Eshopworld.Tests.Core;
using FluentAssertions;
using Xunit;

public class RunbookVmssScalingTests : BaseApiTests
{
    [Fact, IsLayer0]
    public async Task RunbookConfigurationIsAccepted()
    {
        await ApiClient.SetFeaturesAsync(new FeatureFlagsConfiguration { ResourceScallersEnabled = true });
        var scaleGroupDefinition = GetScaleGroupWithRunbook();

        //act
        await ApiClient.SetDefinitionAsync("sg", body: scaleGroupDefinition);
    }


    [Fact, IsLayer0]
    public void MissingAutomationAccountsDefinitions()
    {
        ApiClient.SetFeatures(new FeatureFlagsConfiguration { ResourceScallersEnabled = true });
        var scaleGroupDefinition = GetScaleGroupWithRunbook();
        scaleGroupDefinition.AutomationAccounts = null;

        //act
        Func<Task> func = () => ApiClient.SetDefinitionAsync("sg", body: scaleGroupDefinition);

        func.Should().Throw<ProblemDetailsException>()
            .Where(x => x.Response.StatusCode == System.Net.HttpStatusCode.BadRequest);
    }

    [Fact, IsLayer0]
    public void ReferencedAccountNotDefined()
    {
        ApiClient.SetFeatures(new FeatureFlagsConfiguration { ResourceScallersEnabled = true });
        var scaleGroupDefinition = GetScaleGroupWithRunbook();
        scaleGroupDefinition.AutomationAccounts[0].Name = "bad";

        //act
        Func<Task> func = () => ApiClient.SetDefinitionAsync("sg", body: scaleGroupDefinition);

        func.Should().Throw<ProblemDetailsException>()
            .Where(x => x.Response.StatusCode == System.Net.HttpStatusCode.BadRequest);
    }

    [Fact, IsLayer0]
    public void DuplicatedNamesOfReferencedAccount()
    {
        ApiClient.SetFeatures(new FeatureFlagsConfiguration { ResourceScallersEnabled = true });
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
    public void DuplicatedResourceIdsOfReferencedAccount()
    {
        ApiClient.SetFeatures(new FeatureFlagsConfiguration { ResourceScallersEnabled = true });
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

    [Fact, IsLayer0]
    public async Task ScaleRunbookVsss()
    {
        ApiClient.SetFeatures(new FeatureFlagsConfiguration { ResourceScallersEnabled = true });
        await ApiClient.SetDefinitionAsync("sg", body: GetScaleGroupWithRunbook());
        var eventId = Guid.NewGuid();
        await ApiClient.SaveScaleEventAsync("sg", eventId, NewScaleEvent(1, 3));

        await AdvanceTimeTo(UtcNow.AddHours(6));
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

}
