using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.Monitor.Fluent;
using Microsoft.Azure.Management.Monitor.Fluent.Models;

namespace Bullfrog.Common
{
    /// <summary>
    /// Extension methods for different Azure managemet fluent SDK.
    /// </summary>
    public static class AzureFluentExtensions
    {
        /// <summary>
        /// Updadates the minimal and default number of instances in the specified autoscale settings profile
        /// </summary>
        /// <param name="azure">The azure management client.</param>
        /// <param name="autoscaleSettingsResourceId">The resource ID of the autoscale settings.</param>
        /// <param name="profileName">The name of the profile to modify.</param>
        /// <param name="instanceCalculator">The function which based on the current profile returns new values of changed profile's properties.</param>
        /// <param name="forceUpdate">Specifies whether the profile should be updated even if the new values equal to existing.</param>
        /// <param name="cancellationToken">The cancellation toke.</param>
        /// <returns></returns>
        public static async Task<IAzure> UpdateAutoscaleProfile(
            this IAzure azure,
            string autoscaleSettingsResourceId,
            string profileName,
            Func<IAutoscaleProfile, (int minInstance, int defaultInstances)> instanceCalculator,
            bool forceUpdate = false,
            CancellationToken cancellationToken = default)
        {
            var autoscale = await azure.AutoscaleSettings.ValidateAccessAsync(autoscaleSettingsResourceId, cancellationToken);
            if (!autoscale.Profiles.TryGetValue(profileName, out var profile))
            {
                throw new Exception($"The profile {profileName} has not been found in the autoscale settings {autoscaleSettingsResourceId}.");
            }

            var (minInstance, defaultInstances) = instanceCalculator(profile);

            if (forceUpdate
                || profile.MinInstanceCount != minInstance
                || profile.DefaultInstanceCount != defaultInstances)
            {
                await autoscale.Update()
                    .UpdateAutoscaleProfile(profileName)
                    .WithMetricBasedScale(minInstance, profile.MaxInstanceCount, defaultInstances)
                    .Parent()
                    .ApplyAsync();
            }

            return azure;
        }

        /// <summary>
        /// Reads the specified autoscale settings to confirm read access.
        /// </summary>
        /// <param name="autoscaleSettings">The autoscale settings collection.</param>
        /// <param name="resourceId">The resource ID of the autoscale settings.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        public static async Task<IAutoscaleSetting> ValidateAccessAsync(this IAutoscaleSettings autoscaleSettings, string resourceId, CancellationToken cancellationToken = default)
        {
            try
            {
                return await autoscaleSettings.GetByIdAsync(resourceId, cancellationToken);
            }
            catch (ErrorResponseException ex)
            {
                var message = ex.Response != null && ex.Response.StatusCode == System.Net.HttpStatusCode.NotFound
                    ? $"The autoscale settings {resourceId} has not been found"
                    : $"Failed to access autoscale settings {resourceId}: {ex.Message}";
                throw new Exception(message);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to access autoscale settings {resourceId}: {ex.Message}");
            }
        }
    }
}
