using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Bullfrog.Common.Models;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;

namespace Bullfrog.Common.DependencyInjection
{
    internal class CosmosDataPlaneAccessValidator : ICosmosAccessValidator<CosmosDbDataPlaneConnection>
    {
        private readonly IConfigurationRoot _configuration;

        public CosmosDataPlaneAccessValidator(IConfigurationRoot configuration)
        {
            _configuration = configuration;
        }

        public async Task<ValidationResult> ConfirmAccess(CosmosDbDataPlaneConnection connection)
        {
            var connectionString = _configuration.GetCosmosAccountConnectionStringIfExists(connection.AccountName);
            if (connectionString == null)
            {
                return new ValidationResult($"A connection string for the account {connection.AccountName} has not found.", new[] { nameof(connection.AccountName) });
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
                        return new ValidationResult($"Failed to access account: {ex.Message}", new[] { nameof(connection.AccountName) });
                    }

                    var database = client.Databases[connection.DatabaseName];

                    try
                    {
                        await database.ReadProvisionedThroughputAsync();
                    }
                    catch (Exception ex)
                    {
                        return new ValidationResult($"Failed to access the database {connection.DatabaseName}: {ex.Message}", new[] { nameof(connection.DatabaseName) });
                    }

                    if (connection.ContainerName != null)
                    {
                        var container = database.Containers[connection.ContainerName];

                        try
                        {
                            await container.ReadProvisionedThroughputAsync();
                        }
                        catch (Exception ex)
                        {
                            return new ValidationResult($"Failed to access the container {connection.ContainerName} in the database {connection.DatabaseName}: {ex.Message}", new[] { nameof(connection.ContainerName) });
                        }
                    }

                    await client.SetProvisionedThrouputAsync(null, connection.DatabaseName, connection.ContainerName);
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
