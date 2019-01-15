using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Bullfrog.Actor.Interfaces.Models;
using Bullfrog.Common;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.Monitor.Fluent;

namespace Bullfrog.Actor.Helpers
{
    class ScaleSetManager : IScaleSetManager
    {
        private readonly IAzure _azure;

        public ScaleSetManager(IAzure azure)
        {
            _azure = azure;
        }

        public async Task SetScale(int size, ScaleSetConfiguration configuration, CancellationToken cancellationToken)
        {
            await UpdateProfile(configuration, profile =>
            {
                if (size > profile.MaxInstanceCount)
                    size = profile.MaxInstanceCount;
                return (size, size);
            }, cancellationToken);
        }

        public async Task Reset(ScaleSetConfiguration configuration, CancellationToken cancellationToken)
        {
            await UpdateProfile(configuration, profile => (configuration.MinInstanceCount, configuration.DefaultInstanceCount), cancellationToken);
        }

        public async Task<Dictionary<string, string[]>> ValidateConfiguration(ScaleSetConfiguration configuration, CancellationToken cancellationToken)
        {
            var validationResults = new Dictionary<string, string[]>();
            IAutoscaleSetting autoscale;
            try
            {
                autoscale = await _azure.AutoscaleSettings.GetByIdAsync(configuration.AutoscaleSettingsResourceId, cancellationToken);
            }
            catch (Microsoft.Azure.Management.Monitor.Fluent.Models.ErrorResponseException ex)
            {
                var message = ex.Response != null & ex.Response.StatusCode == System.Net.HttpStatusCode.NotFound
                    ? $"The autoscale settings {configuration.AutoscaleSettingsResourceId} has not been found"
                    : $"Failed to access autoscale settings {configuration.AutoscaleSettingsResourceId}: {ex.Message}";
                validationResults.Append(nameof(configuration.AutoscaleSettingsResourceId), message);
                return validationResults;
            }
            catch (Exception ex)
            {
                validationResults.Append(nameof(configuration.AutoscaleSettingsResourceId),
                    $"Failed to access autoscale settings {configuration.AutoscaleSettingsResourceId}: {ex.Message}");
                return validationResults;
            }

            if (!autoscale.Profiles.TryGetValue(configuration.ProfileName, out var profile))
            {
                validationResults.Append(nameof(configuration.ProfileName),
                    $"The autoscale settings {configuration.AutoscaleSettingsResourceId} had no \"{configuration.ProfileName}\" profile");
                return validationResults;
            }

            if (configuration.MinInstanceCount > profile.MaxInstanceCount)
            {
                validationResults.Append(nameof(configuration.MinInstanceCount),
                    $"The specified min instance count {configuration.MinInstanceCount} is higher than the profile's max instance count {profile.MaxInstanceCount}");
            }

            if (configuration.DefaultInstanceCount < configuration.MinInstanceCount)
            {
                validationResults.Append(nameof(configuration.DefaultInstanceCount),
                    $"The specified default instance count {configuration.DefaultInstanceCount} is lower than the specified min instance count {configuration.MinInstanceCount}");
            }

            if (configuration.DefaultInstanceCount > profile.MaxInstanceCount)
            {
                validationResults.Append(nameof(configuration.DefaultInstanceCount),
                    $"The specified default instance count {configuration.DefaultInstanceCount} is higher than the profile's max instance count {profile.MaxInstanceCount}");
            }

            return validationResults.Count == 0 ? null : validationResults;
        }

        private async Task UpdateProfile(
            ScaleSetConfiguration configuration,
            Func<IAutoscaleProfile, (int minInstance, int defaultInstances)> newInstanceCounts,
            CancellationToken cancellationToken)
        {
            // TODO: add exception handling
            var autoscale = await _azure.AutoscaleSettings.GetByIdAsync(configuration.AutoscaleSettingsResourceId, cancellationToken);
            if (!autoscale.Profiles.TryGetValue(configuration.ProfileName, out var profile))
            {
                throw new Exception();
            }

            var (minInstance, defaultInstances) = newInstanceCounts(profile);

            if (profile.MinInstanceCount != minInstance || profile.DefaultInstanceCount != defaultInstances)
            {
                await autoscale.Update()
                    .UpdateAutoscaleProfile(configuration.ProfileName)
                    .WithMetricBasedScale(minInstance, profile.MaxInstanceCount, defaultInstances)
                    .Parent()
                    .ApplyAsync();
            }
        }
    }
}
