using System;
using Microsoft.Extensions.Configuration;

namespace Bullfrog.Common
{
    /// <summary>
    /// Configuration extensions methods.
    /// </summary>
    public static class ConfigurationRootExtensions
    {
        /// <summary>
        /// Gets the connection string of the specified Cosmos DB account.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        /// <param name="accountName">The Cosmos DB account.</param>
        /// <returns>The connection string.</returns>
        /// <exception cref="BullfrogException">The connection string has not been found.</exception>
        public static string GetCosmosAccountConnectionString(this IConfigurationRoot configuration, string accountName)
        {
            var connectionString = configuration.GetCosmosAccountConnectionStringIfExists(accountName);
            if (String.IsNullOrWhiteSpace(connectionString))
            {
                throw new BullfrogException($"The connection string for the Cosmos account {accountName} has not been found");
            }

            return connectionString;
        }

        /// <summary>
        /// Gets the connection string of the specified Cosmos DB account if it has been configured.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        /// <param name="accountName">The Cosmos DB account.</param>
        /// <returns>The connection string or null.</returns>
        public static string GetCosmosAccountConnectionStringIfExists(this IConfigurationRoot configuration, string accountName)
        {
            string GetConfig()
            {
                var cosmosConnectionStringsSection = configuration.GetSection("cm:cosmos-connection");
                return cosmosConnectionStringsSection[accountName];
            }

            var connectionString = GetConfig();
            if (String.IsNullOrWhiteSpace(connectionString))
            {
                configuration.Reload();
                connectionString = GetConfig();
            }

            return connectionString;
        }
    }
}
