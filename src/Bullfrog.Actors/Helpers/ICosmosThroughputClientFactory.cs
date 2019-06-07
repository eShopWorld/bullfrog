using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Configuration;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Bullfrog.Actors.Interfaces.Models;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;

namespace Bullfrog.Actors.Helpers
{
    public interface ICosmosThroughputClientFactory
    {
        ICosmosThroughputClient ClientClient(CosmosConfiguration cosmosConfiguration);

        Task<ValidationResult> Validate(CosmosConfiguration cosmosConfiguration);
    }

    public interface ICosmosThroughputClient7777
    {
        Task<CosmosThroughput> Get();

        Task Set(int throughput);
    }

    internal interface ICosmosThroughputClientInitializer : ICosmosThroughputClient
    {
        void UseConfiguration(CosmosConfiguration configuration);

        Task<ValidationResult> ValidateConfiguration();
    }

    internal class CosmosThroughputClientFactory : ICosmosThroughputClientFactory
    {
        public async Task<ValidationResult> Validate(CosmosConfiguration cosmosConfiguration)
        {
            ICosmosThroughputClientInitializer client;
            if (!string.IsNullOrWhiteSpace(cosmosConfiguration.AccountName))
            {
                if (!string.IsNullOrWhiteSpace(cosmosConfiguration.CosmosDbResourceId))
                {
                    return new ValidationResult($"The {nameof(cosmosConfiguration.CosmosDbResourceId)} cannot be used together with {nameof(cosmosConfiguration.AccountName)}.", new[] { nameof(cosmosConfiguration.CosmosDbResourceId) });
                }

                client = GetDataPlaneClient();
            }
            else
            {

                if (string.IsNullOrWhiteSpace(cosmosConfiguration.CosmosDbResourceId))
                {
                    return new ValidationResult($"Either {nameof(cosmosConfiguration.CosmosDbResourceId)} or {nameof(cosmosConfiguration.AccountName)} must be set.", new[] { nameof(cosmosConfiguration.AccountName) });
                }

                client = GetControlPlaneClient();
            }

            client.UseConfiguration(cosmosConfiguration);
            return await client.ValidateConfiguration();
        }

        private ICosmosThroughputClientInitializer GetDataPlaneClient()
        {

        }

        private ICosmosThroughputClientInitializer GetControlPlaneClient()
        {

        }

        public ICosmosThroughputClient ClientClient(CosmosConfiguration cosmosConfiguration)
        {
            ICosmosThroughputClientInitializer client;
            if (!string.IsNullOrWhiteSpace(cosmosConfiguration.AccountName))
            {
                client = GetDataPlaneClient();
            }
            else
            {
                client = GetControlPlaneClient();
            }

            client.UseConfiguration(cosmosConfiguration);
            return client;
        }
    }

    internal class DataPlaneCosmosClient : ICosmosThroughputClientInitializer
    {
        private readonly IConfigurationRoot _configuration;
        private CosmosConfiguration _cosmosConfiguration;

        public DataPlaneCosmosClient(IConfigurationRoot configuration)
        {
            _configuration = configuration;
        }

        public Task<CosmosThroughput> Get()
        {
            throw new NotImplementedException();
        }

        public Task Set(int throughput)
        {
            throw new NotImplementedException();
        }

        public void UseConfiguration(CosmosConfiguration configuration)
        {
            if (_cosmosConfiguration != null)
                throw new InvalidOperationException("Only one configuration may be used.");

            _cosmosConfiguration = configuration;
        }

        public async Task<ValidationResult> ValidateConfiguration()
        {
            var connectionString = _configuration.GetCosmosAccountConnectionString(_cosmosConfiguration.AccountName);
            if (connectionString == null)
            {
                _configuration.Reload();
                connectionString = _configuration.GetCosmosAccountConnectionString(_cosmosConfiguration.AccountName);
            }
            if (connectionString == null)
            {
                return new ValidationResult($"A connection string for the account {_cosmosConfiguration.AccountName} has not found.", new[] { nameof(_cosmosConfiguration.AccountName) });
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
                        return new ValidationResult($"Failed to access account: {ex.Message}", new[] { nameof(_cosmosConfiguration.AccountName) });
                    }

                    var database = client.Databases[_cosmosConfiguration.DatabaseName];

                    try
                    {
                        await database.ReadProvisionedThroughputAsync();
                    }
                    catch (Exception ex)
                    {
                        return new ValidationResult($"Failed to access the database {_cosmosConfiguration.DatabaseName}: {ex.Message}", new[] { nameof(_cosmosConfiguration.DatabaseName) });
                    }

                    if (_cosmosConfiguration.ContainerName != null)
                    {
                        var container = database.Containers[_cosmosConfiguration.ContainerName];

                        try
                        {
                            await container.ReadProvisionedThroughputAsync();
                        }
                        catch (Exception ex)
                        {
                            return new ValidationResult($"Failed to access the container {_cosmosConfiguration.ContainerName} in the database {_cosmosConfiguration.DatabaseName}: {ex.Message}", new[] { nameof(_cosmosConfiguration.ContainerName) });
                        }
                    }

                    await client.SetProvisionedThrouputAsync(null, _cosmosConfiguration.DatabaseName, _cosmosConfiguration.ContainerName);
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
