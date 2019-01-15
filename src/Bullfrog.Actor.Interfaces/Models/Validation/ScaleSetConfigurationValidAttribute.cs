using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Bullfrog.Common;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.Monitor.Fluent;

namespace Bullfrog.Actor.Interfaces.Models.Validation
{
    public class ScaleSetConfigurationValidAttribute : ValidationAttribute
    {
        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            var azure = (IAzure)validationContext.GetService(typeof(IAzure));

            return IsValid((ScaleSetConfiguration)value, azure).GetAwaiter().GetResult();
        }

        private async Task<ValidationResult> IsValid(ScaleSetConfiguration value, IAzure azure)
        {
            IAutoscaleSetting autoscale;
            try
            {
                autoscale = await azure.AutoscaleSettings.ValidateAccessAsync(value.AutoscaleSettingsResourceId);
            }
            catch (Exception ex)
            {
                return new ValidationResult(ex.Message, new[] { nameof(value.AutoscaleSettingsResourceId) });
            }

            if (!autoscale.Profiles.TryGetValue(value.ProfileName, out var profile))
            {
                var message = $"The autoscale settings {value.AutoscaleSettingsResourceId} had no \"{value.ProfileName}\" profile";
                return new ValidationResult(message, new[] { nameof(value.ProfileName) });
            }

            try
            {
                var parameters = new UpdateAutoscaleProfileParameters
                {
                    AutoscaleSettingsResourceId = value.AutoscaleSettingsResourceId,
                    ProfileName = value.ProfileName,
                    ForceUpdate = true,
                    NewInstanceCountCalculator = pr => (pr.MinInstanceCount, pr.DefaultInstanceCount), // keep the same
                };
                await azure.UpdateAutoscaleProfile(parameters);
            }
            catch (Exception ex)
            {
                return new ValidationResult(
                    $"Failed to update autoscale settings {value.AutoscaleSettingsResourceId}: {ex.Message}",
                    new[] { nameof(value.AutoscaleSettingsResourceId) });
            }

            if (value.MinInstanceCount > profile.MaxInstanceCount)
            {
                return new ValidationResult(
                    $"The specified min instance count {value.MinInstanceCount} is higher than the profile's max instance count {profile.MaxInstanceCount}",
                    new[] { nameof(value.MinInstanceCount) });
            }

            if (value.DefaultInstanceCount < value.MinInstanceCount)
            {
                return new ValidationResult(
                    $"The specified default instance count {value.DefaultInstanceCount} is lower than the specified min instance count {value.MinInstanceCount}",
                    new[] { nameof(value.DefaultInstanceCount) });
            }

            if (value.DefaultInstanceCount > profile.MaxInstanceCount)
            {
                return new ValidationResult(
                    $"The specified default instance count {value.DefaultInstanceCount} is higher than the profile's max instance count {profile.MaxInstanceCount}",
                    new[] { nameof(value.DefaultInstanceCount) });
            }

            return ValidationResult.Success;
        }
    }
}
