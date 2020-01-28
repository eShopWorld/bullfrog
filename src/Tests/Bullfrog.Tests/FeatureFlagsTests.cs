using System.Reflection.Emit;
using System.Threading.Tasks;
using Client;
using Client.Models;
using Eshopworld.Tests.Core;
using FluentAssertions;
using Xunit;

public class FeatureFlagsTests : BaseApiTests
{
    [Theory, IsLayer0]
    [InlineData(true)]
    [InlineData(false)]
    public void FeatureFlagIsPreserved(bool isEnabled)
    {
        ApiClient.SetFeatures(new FeatureFlagsConfiguration
        {
            ResourceScallersEnabled = isEnabled,
        });

        var featureFlags = ApiClient.GetFeatureFlags();

        featureFlags.ResourceScallersEnabled.Should().Be(isEnabled);
    }

    [Theory, IsLayer0]
    [InlineData(true)]
    [InlineData(false)]
    public void ResourceScalersEnabledFlagIsNotChangedWhenSetNotProvided(bool isEnabled)
    {
        ApiClient.SetFeatures(new FeatureFlagsConfiguration
        {
            ResourceScallersEnabled = isEnabled,
        });

        ApiClient.SetFeatures(new FeatureFlagsConfiguration
        {
            ResourceScallersEnabled = null, // preserve the previous value
        });

        var featureFlags = ApiClient.GetFeatureFlags();

        featureFlags.ResourceScallersEnabled.Should().Be(isEnabled);
    }

    [Fact, IsLayer0]
    public void DefaultResourceScalersEnabledValueIsNotDefined()
    {
        var featureFlags = ApiClient.GetFeatureFlags();

        featureFlags.ResourceScallersEnabled.Should().BeNull();
    }
}
