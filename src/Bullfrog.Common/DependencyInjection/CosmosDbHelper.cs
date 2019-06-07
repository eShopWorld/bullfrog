using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;

namespace Bullfrog.Common.DependencyInjection
{
    internal class CosmosDbHelper : ICosmosDbHelper
    {
        private readonly IConfigurationRoot _configuration;

        public CosmosDbHelper(IConfigurationRoot configuration)
        {
            _configuration = configuration;
        }

        public async Task<ValidationResult> ValidateConfiguration(CosmosDbConfiguration configuration)
        {
            return await ValidateDataPlane(configuration);
        }

        private async Task<ValidationResult> ValidateDataPlane(CosmosDbConfiguration configuration)
        {
            var connectionString = _configuration.GetCosmosAccountConnectionStringIfExists(configuration.AccountName);
            if (connectionString == null)
            {
                return new ValidationResult($"A connection string for the account {configuration.AccountName} has not found.", new[] { nameof(configuration.AccountName) });
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
                        return new ValidationResult($"Failed to access account: {ex.Message}", new[] { nameof(configuration.AccountName) });
                    }

                    var database = client.Databases[configuration.DatabaseName];

                    try
                    {
                        await database.ReadProvisionedThroughputAsync();
                    }
                    catch (Exception ex)
                    {
                        return new ValidationResult($"Failed to access the database {configuration.DatabaseName}: {ex.Message}", new[] { nameof(configuration.DatabaseName) });
                    }

                    if (configuration.ContainerName != null)
                    {
                        var container = database.Containers[configuration.ContainerName];

                        try
                        {
                            await container.ReadProvisionedThroughputAsync();
                        }
                        catch (Exception ex)
                        {
                            return new ValidationResult($"Failed to access the container {configuration.ContainerName} in the database {configuration.DatabaseName}: {ex.Message}", new[] { nameof(configuration.ContainerName) });
                        }
                    }

                    await client.SetProvisionedThrouputAsync(null, configuration.DatabaseName, configuration.ContainerName);
                }
            }
            catch (Exception ex)
            {
                return new ValidationResult($"Failed to validation configuration: {ex.Message}");
            }

            return ValidationResult.Success;
        }
    }
}
