using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Client;
using Client.Models;
using Eshopworld.Tests.Core;
using FluentAssertions;
using Xunit;

public class ScaleGroupStateTests : BaseApiTests
{
    [Fact, IsLayer0]
    public void EmptyScaleGroupState()
    {
        CreateScaleGroup();

        var state = ApiClient.GetCurrentState("sg");

        state.Should().BeEquivalentTo(new ScaleGroupState
        {
            Regions = new ScaleRegionState[0],
        });
    }

    [Fact, IsLayer0]
    public void UnknownScaleGroupState()
    {
        CreateScaleGroup();
 
        Action call = () => ApiClient.GetCurrentState("sg11");

        call.Should().Throw<ProblemDetailsException>()
            .Which.Response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact, IsLayer0]
    public async Task StateWithActiveScaleEvent()
    {
        RegisterDefaultScalers();
        CreateScaleGroup();
        ApiClient.SaveScaleEvent("sg", Guid.NewGuid(), NewScaleEvent(1, 4));
        var now = UtcNow;
        await AdvanceTimeTo(now.AddHours(2));

        var state = ApiClient.GetCurrentState("sg");

        state.Should().BeEquivalentTo(new ScaleGroupState
        {
            Regions = new []
            {
                new ScaleRegionState
                {
                    Name = "eu",
                    Scale = 0,
                    RequestedScale = 10,
                    WasScaledUpAt = now.AddHours(1),
                    WillScaleDownAt = now.AddHours(4),
                    ScaleSetState = new Dictionary<string, double?>
                    {
                        ["s"] = 0.0,
                    },
                }
            }
        });
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
                    ScaleSets = new List<ScaleSetConfiguration>
                    {
                        new ScaleSetConfiguration
                        {
                            Name = "s",
                            AutoscaleSettingsResourceId = GetAutoscaleSettingResourceId(),
                            ProfileName = "pr",
                            LoadBalancerResourceId = GetLoadBalancerResourceId(),
                            HealthPortPort = 9999,
                            RequestsPerInstance = 100,
                        },
                    },
                }
            },
        };
    }
}
