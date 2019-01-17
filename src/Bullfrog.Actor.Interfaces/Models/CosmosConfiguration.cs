using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Bullfrog.Actor.Interfaces.Models.Validation;
using Bullfrog.Common;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;

namespace Bullfrog.Actor.Interfaces.Models
{
    public class CosmosConfiguration : IValidatableObject
    {
        [Required]
        public string Name { get; set; }

        [Required]
        public string AccountName { get; set; }

        [Required]
        public string DatabaseName { get; set; }

        public string ContainerName { get; set; }

        [ValueIs(ValueComparision.GreaterThen, Value = 0)]
        public decimal RequestsPerRU { get; set; }

        [CosmosRU]
        public int MinimumRU { get; set; }

        [CosmosRU]
        [ValueIs(ValueComparision.GreaterThanOrEqualTo, PropertyValue = nameof(MinimumRU))]
        public int MaximumRU { get; set; }

        #region Validation

        IEnumerable<ValidationResult> IValidatableObject.Validate(ValidationContext validationContext)
        {
            var configuration = (IConfigurationRoot)validationContext.GetService(typeof(IConfigurationRoot));

            yield return IsValidAsync(configuration).GetAwaiter().GetResult();
        }

        private async Task<ValidationResult> IsValidAsync(IConfigurationRoot configuration)
        {
            // TODO: fix somehow this code duplication
            var cosmosConnectionStringsSection = configuration.GetSection("Bullfrog").GetSection("Cosmos");

            var connectionString = cosmosConnectionStringsSection[AccountName];
            if (connectionString == null)
            {
                return new ValidationResult($"Connection string for account {AccountName} not found.", new[] { nameof(AccountName) });
            }

            try
            {
                using (var client = new CosmosClient(connectionString))
                {
                    try
                    {
                        await client.GetAccountSettingsAsync();
                    }
                    catch (Exception ex)
                    {
                        return new ValidationResult($"Failed to access account: {ex.Message}", new[] { nameof(AccountName) });
                    }

                    var database = client.Databases[DatabaseName];

                    try
                    {
                        await database.ReadProvisionedThroughputAsync();
                    }
                    catch (Exception ex)
                    {
                        return new ValidationResult($"Failed to access the database {DatabaseName}: {ex.Message}", new[] { nameof(DatabaseName) });
                    }

                    if (ContainerName != null)
                    {
                        var container = database.Containers[ContainerName];

                        try
                        {
                            await container.ReadProvisionedThroughputAsync();
                        }
                        catch (Exception ex)
                        {
                            return new ValidationResult($"Failed to access the container {ContainerName} in the database {DatabaseName}: {ex.Message}", new[] { nameof(ContainerName) });
                        }
                    }

                    await client.SetProvisionedThrouputAsync(null, DatabaseName, ContainerName);
                }
            }
            catch (Exception ex)
            {
                return new ValidationResult($"Failed to validation configuration: {ex.Message}");
            }

            return ValidationResult.Success;
        }

        #endregion
    }
}
