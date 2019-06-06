using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;

namespace Bullfrog.Common.Cosmos
{
    internal class DataPlaneCosmosThroughputClientFactory : ICosmosThroughputClientFactory
    {
        private readonly IConfigurationRoot _configuration;

        public DataPlaneCosmosThroughputClientFactory(IConfigurationRoot configuration)
        {
            _configuration = configuration;
        }

        public ICosmosThroughputClient CreateClientClient(CosmosDbConfiguration cosmosConfiguration)
        {
            return new DataPlaneCosmosClient(_configuration, )
        }

        public async Task<ValidationResult> Validate(CosmosDbConfiguration cosmosConfiguration)
        {
            var connectionString = _configuration.GetCosmosAccountConnectionString(cosmosConfiguration.AccountName);
            if (connectionString == null)
            {
                _configuration.Reload();
                connectionString = _configuration.GetCosmosAccountConnectionString(cosmosConfiguration.AccountName);
            }
            if (connectionString == null)
            {
                return new ValidationResult($"A connection string for the account {cosmosConfiguration.AccountName} has not found.", new[] { nameof(CosmosDbConfiguration.AccountName) });
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
                        return new ValidationResult($"Failed to access account: {ex.Message}", new[] { nameof(CosmosDbConfiguration.AccountName) });
                    }

                    var database = client.Databases[cosmosConfiguration.DatabaseName];

                    try
                    {
                        await database.ReadProvisionedThroughputAsync();
                    }
                    catch (Exception ex)
                    {
                        return new ValidationResult($"Failed to access the database {cosmosConfiguration.DatabaseName}: {ex.Message}", new[] { nameof(CosmosDbConfiguration.DatabaseName) });
                    }

                    if (cosmosConfiguration.ContainerName != null)
                    {
                        var container = database.Containers[cosmosConfiguration.ContainerName];

                        try
                        {
                            await container.ReadProvisionedThroughputAsync();
                        }
                        catch (Exception ex)
                        {
                            return new ValidationResult($"Failed to access the container {cosmosConfiguration.ContainerName} in the database {CosmosDbConfiguration.DatabaseName}: {ex.Message}", new[] { nameof(CosmosDbConfiguration.ContainerName) });
                        }
                    }

                    await client.SetProvisionedThrouputAsync(null, cosmosConfiguration.DatabaseName, cosmosConfiguration.ContainerName);
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
