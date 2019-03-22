using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Bullfrog.Actors.Interfaces.Models.Validation;
using Bullfrog.Common;
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
        public int MinInstanceCount { get; set; }

        /// <summary>
        /// The default number of instances defined in the profile.
        /// </summary>
        [Range(1, 1000)]
        [ValueIs(ValueComparison.GreaterThanOrEqualTo, PropertyValue = nameof(MinInstanceCount))]
        public int DefaultInstanceCount { get; set; }

        /// <summary>
        /// The number (might be partial) of VM instances which are not used to handle requests.
        /// </summary>
        [ValueIs(ValueComparison.GreaterThanOrEqualTo, Value = 0)]
        [ValueIs(ValueComparison.LessThanOrEqualTo, PropertyValue = nameof(MinInstanceCount))]
        public decimal ReservedInstances { get; set; }

        #region Validation

        IEnumerable<ValidationResult> IValidatableObject.Validate(ValidationContext validationContext)
        {
            var azure = (IAzure)validationContext.GetService(typeof(IAzure));

            yield return IsValidAsync(azure).GetAwaiter().GetResult();
        }

        private async Task<ValidationResult> IsValidAsync(IAzure azure)
        {
            IAutoscaleSetting autoscale;
            try
            {
                autoscale = await azure.AutoscaleSettings.ValidateAccessAsync(AutoscaleSettingsResourceId);
            }
            catch (Exception ex)
            {
                return new ValidationResult(ex.Message, new[] { nameof(AutoscaleSettingsResourceId) });
            }

            if (!autoscale.Profiles.TryGetValue(ProfileName, out var profile))
            {
                var message = $"The autoscale settings {AutoscaleSettingsResourceId} had no \"{ProfileName}\" profile";
                return new ValidationResult(message, new[] { nameof(ProfileName) });
            }

            try
            {
                await azure.UpdateAutoscaleProfile(
                    AutoscaleSettingsResourceId,
                    ProfileName,
                    pr => (pr.MinInstanceCount, pr.DefaultInstanceCount), // keep values the same
                    forceUpdate: true);
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

            if (DefaultInstanceCount > profile.MaxInstanceCount)
            {
                return new ValidationResult(
                    $"The specified default instance count {DefaultInstanceCount} is higher than the profile's max instance count {profile.MaxInstanceCount}",
                    new[] { nameof(DefaultInstanceCount) });
            }

            try
            {
                await azure.MetricDefinitions.ListByResourceAsync(LoadBalancerResourceId);
            }
            catch (Exception ex)
            {
                return new ValidationResult(
                                   $"Failed to read metric definitions of {LoadBalancerResourceId}: {ex.Message}",
                                   new[] { nameof(LoadBalancerResourceId) });
            }

            return ValidationResult.Success;
        }

        #endregion
    }
}
