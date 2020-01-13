using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Bullfrog.Actors.Interfaces.Models.Validation;
using Bullfrog.Common;
using Bullfrog.Common.Helpers;
using Bullfrog.Common.Models.Validation;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.Monitor.Fluent;

namespace Bullfrog.Actors.Interfaces.Models
{
    /// <summary>
    /// Configuration of virtual machine scale set which is part of the scale group.
    /// </summary>
    public class ScaleSetConfiguration : IValidatableObject
    {
        /// <summary>
        /// The name used as an identifier of this VM scale set configuration.
        /// </summary>
        [Required]
        public string Name { get; set; }

        /// <summary>
        /// The resource id of the autoscale settings which controls virtual machine scale set scaling.
        /// </summary>
        [Required]
        [AzureResourceId]
        public string AutoscaleSettingsResourceId { get; set; }

        /// <summary>
        /// The name of the profile of autoscale settings which is used to control VMSS scaling.
        /// </summary>
        [Required]
        public string ProfileName { get; set; }

        /// <summary>
        /// The resource id of a load balancer of virtual machine scale set.
        /// </summary>
        [Required]
        [AzureResourceId]
        public string LoadBalancerResourceId { get; set; }

        /// <summary>
        /// The port used for health probes by a load balancer which should be used to check
        /// availability of VMs in the scale set.
        /// </summary>
        [Range(1, 0xffff)]
        public int HealthPortPort { get; set; }

        /// <summary>
        /// The number of requests per VMSS instance
        /// </summary>
        [Range(0, 1000_000_000)]
        public int RequestsPerInstance { get; set; }

        /// <summary>
        /// The minimal number of instances defined in the profile.
        /// </summary>
        [Range(1, 1000)]
        public int? MinInstanceCount { get; set; }

        /// <summary>
        /// The number (might be partial) of VM instances which are not used to handle requests.
        /// </summary>
        [ValueIs(ValueComparison.GreaterThanOrEqualTo, Value = 0)]
        [ValueIs(ValueComparison.LessThanOrEqualTo, PropertyValue = nameof(MinInstanceCount))]
        public decimal ReservedInstances { get; set; }

        /// <summary>
        /// The optional configuration of runbook used to change the scale of the scale set.
        /// </summary>
        public ScaleSetRunbookConfiguration Runbook { get; set; }

        #region Validation

        IEnumerable<ValidationResult> IValidatableObject.Validate(ValidationContext validationContext)
        {
            var authenticated = (Azure.IAuthenticated)validationContext.GetService(typeof(Azure.IAuthenticated));
            var scaleSetMonitor = (ScaleSetMonitor)validationContext.GetService(typeof(ScaleSetMonitor));

            yield return IsValidAsync(authenticated, scaleSetMonitor).GetAwaiter().GetResult();
        }

        private async Task<ValidationResult> IsValidAsync(Azure.IAuthenticated authenticated, ScaleSetMonitor monitor)
        {
            IAzure azure;
            IAutoscaleSetting autoscale;
            try
            {
                azure = authenticated.WithSubscriptionFor(AutoscaleSettingsResourceId);
                autoscale = await azure.AutoscaleSettings.ValidateAccessAsync(AutoscaleSettingsResourceId);
            }
            catch (Exception ex)
            {
                return new ValidationResult(ex.Message, new[] { nameof(AutoscaleSettingsResourceId) });
            }

            if (!autoscale.Profiles.TryGetValue(ProfileName, out var profile))
            {
                var message = $"The autoscale setting {AutoscaleSettingsResourceId} had no \"{ProfileName}\" profile.";
                return new ValidationResult(message, new[] { nameof(ProfileName) });
            }

            if(profile.Rules.Count == 0)
            {
                var message = $"The profile {ProfileName} in the autoscale setting {AutoscaleSettingsResourceId} is not based on a metric.";
                return new ValidationResult(message, new[] { nameof(ProfileName) });
            }

            if (Runbook != null)
            {
                // When runbook is used to change autoscale setting Bullfrog only needs a read access (validated above).
                return ValidationResult.Success;
            }

            // If scaling is not done using a runbook ensure Bullfrog can update and save the autoscale settings.
            try
            {
                await autoscale.Update().ApplyAsync();
            }
            catch (Exception ex)
            {
                return new ValidationResult(
                    $"Failed to update autoscale settings {AutoscaleSettingsResourceId}: {ex.Message}",
                    new[] { nameof(AutoscaleSettingsResourceId) });
            }

            if (MinInstanceCount > profile.MaxInstanceCount)
            {
                return new ValidationResult(
                    $"The specified min instance count {MinInstanceCount} is higher than the profile's max instance count {profile.MaxInstanceCount}",
                    new[] { nameof(MinInstanceCount) });
            }

            return await monitor.ValidateAccess(new LoadBalancerConfiguration
            {
                HealthProbePort = HealthPortPort,
                LoadBalancerResourceId = LoadBalancerResourceId,
            });
        }

        #endregion
    }
}
