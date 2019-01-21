using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Bullfrog.Actors.Interfaces.Models.Validation;
using Bullfrog.Common;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;

namespace Bullfrog.Actors.Interfaces.Models
{
    /// <summary>
    /// Defines a Cosmos DB database or a container which is should be scaled to handle requested throughput. 
    /// </summary>
    public class CosmosConfiguration : IValidatableObject
    {
        /// <summary>
        /// The name used as an identifier of this Cosmos DB instance.
        /// </summary>
        [Required]
        public string Name { get; set; }

        /// <summary>
        /// The Cosmos DB account name.
        /// </summary>
        [Required]
        public string AccountName { get; set; }

        /// <summary>
        /// The Cosmos DB database name.
        /// </summary>
        [Required]
        public string DatabaseName { get; set; }

        /// <summary>
        /// The optional name of the container in the Cosmos DB database.
        /// </summary>
        public string ContainerName { get; set; }

        /// <summary>
        /// The number of requests per configured RU which on avarage can be processed per second.
        /// </summary>
        [ValueIs(ValueComparision.GreaterThen, Value = 0)]
        public decimal RequestsPerRU { get; set; }

        /// <summary>
        /// The minimal value of RU used when there are no active events.
        /// </summary>
        [CosmosRU]
        public int MinimumRU { get; set; }

        /// <summary>
        /// The maximal value of RU. No scaling operation will exceed it.
        /// </summary>
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
            var connectionString = configuration.GetCosmosAccountConnectionString(AccountName);
            if (connectionString == null)
            {
                return new ValidationResult($"A connection string for the account {AccountName} has not found.", new[] { nameof(AccountName) });
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
