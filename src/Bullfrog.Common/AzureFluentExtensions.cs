using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.Monitor.Fluent;
using Microsoft.Azure.Management.Monitor.Fluent.AutoscaleProfile.UpdateDefinition;
using Microsoft.Azure.Management.Monitor.Fluent.Models;

namespace Bullfrog.Common
{
    /// <summary>
    /// Extension methods for different Azure managemet fluent SDK.
    /// </summary>
    public static class AzureFluentExtensions
    {
        public const string BullfrogProfileName = "BullfrogProfile";

        public static async Task<(int Instances, bool ProfileChanged, IAutoscaleSetting autoscaleSetting)> SaveBullfrogProfile(
            this IAzure azure,
            string autoscaleSettingsResourceId,
            string defaultProfileName,
            int instances,
            DateTimeOffset start,
            DateTimeOffset end,
            CancellationToken cancellationToken = default)
        {
            var autoscale = await azure.AutoscaleSettings.ValidateAccessAsync(autoscaleSettingsResourceId, cancellationToken);
            if (!autoscale.Profiles.TryGetValue(defaultProfileName, out var defaultProfile))
            {
                throw new BullfrogException($"The profile {defaultProfileName} has not been found in the autoscale settings {autoscaleSettingsResourceId}.");
            }

            var minInstances = Math.Max(defaultProfile.MinInstanceCount, instances);
            minInstances = Math.Min(defaultProfile.MaxInstanceCount, minInstances);
            var defaultInstances = Math.Max(defaultProfile.DefaultInstanceCount, minInstances);

            bool profileModified = false;
            if (autoscale.Profiles.TryGetValue(BullfrogProfileName, out var bullfrogProfile))
            {
                if (bullfrogProfile.MinInstanceCount != minInstances
                    || bullfrogProfile.DefaultInstanceCount != defaultInstances
                    || bullfrogProfile.FixedDateSchedule == null
                    || bullfrogProfile.FixedDateSchedule.End != end.UtcDateTime )
                {
                    autoscale = await autoscale.Update()
                         .UpdateAutoscaleProfile(BullfrogProfileName)
                         .WithMetricBasedScale(minInstances, defaultProfile.MaxInstanceCount, defaultInstances)
                         .WithFixedDateSchedule("UTC", start.UtcDateTime, end.UtcDateTime)
                         .Parent()
                         .ApplyAsync();
                    profileModified = true;
                }
            }
            else
            {
                autoscale = await autoscale.Update()
                    .DefineAutoscaleProfile(BullfrogProfileName)
                    .WithMetricBasedScale(minInstances, defaultProfile.MaxInstanceCount, defaultInstances)
                    .AddRules(defaultProfile.Rules)
                    .WithFixedDateSchedule("UTC", start.UtcDateTime, end.UtcDateTime)
                    .Attach()
                    .ApplyAsync();
                    profileModified = true;
            }

            return (minInstances, profileModified, autoscale);
        }

        public static async Task<(IAutoscaleSetting autoscaleSettings, bool changed)> RemoveBullfrogProfile(
            this IAzure azure,
            string autoscaleSettingsResourceId,
            CancellationToken cancellationToken = default)
        {
            var autoscale = await azure.AutoscaleSettings.ValidateAccessAsync(autoscaleSettingsResourceId, cancellationToken);
            if (autoscale.Profiles.ContainsKey(BullfrogProfileName))
            {
                var updated = await autoscale.Update()
                    .WithoutAutoscaleProfile(BullfrogProfileName)
                    .ApplyAsync();
                return (updated, true);
            }

            return (autoscale, false);
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
                    ? $"The autoscale settings {resourceId} has not been found ({ex.Response?.Content})"
                    : $"Failed to access autoscale settings {resourceId}: {ex.Message} ({ex.Response?.Content})";
                throw new BullfrogException(message);
            }
            catch (Exception ex)
            {
                throw new BullfrogException($"Failed to access autoscale settings {resourceId}: {ex.Message}");
            }
        }

        private static IWithScaleRuleOptional AddRules(this IWithScaleRule profile, IEnumerable<IScaleRule> rules)
        {
            IWithScaleRuleOptional ruleOptional = null;
            foreach (var rule in rules)
            {
                ruleOptional = (ruleOptional?.DefineScaleRule() ?? profile.DefineScaleRule())
                    .WithMetricSource(rule.MetricSource)
                    .WithMetricName(rule.MetricName)
                    .WithStatistic(rule.Duration, rule.Frequency, rule.FrequencyStatistic)
                    .WithCondition(rule.TimeAggregation, rule.Condition, rule.Threshold)
                    .WithScaleAction(rule.ScaleDirection, rule.ScaleType, rule.ScaleInstanceCount, rule.CoolDown)
                    .Attach();
            }

            if (ruleOptional == null)
                throw new ArgumentException("The list of rules must not be empty.");

            return ruleOptional;
        }
    }
}
