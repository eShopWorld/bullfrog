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
        /// Gets the connection to the specified Cosmos DB account from configuration.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        /// <param name="accountName">The Cosmos DB account.</param>
        /// <param name="isOptional">If true null is returned if connection string is returned.</param>
        /// <returns>The connection string or null.</returns>
        public static string GetCosmosAccountConnectionString(this IConfigurationRoot configuration, string accountName, bool isOptional = false)
        {
            string GetConfig()
            {
                var cosmosConnectionStringsSection = configuration.GetSection("Bullfrog").GetSection("Cosmos");
                return cosmosConnectionStringsSection[accountName];
            }

            var connectionString = GetConfig();
            if (connectionString == null)
            {
                configuration.Reload();
                connectionString = GetConfig();
            }
            if (!isOptional && connectionString == null)
            {
                throw new BullfrogException($"The connection string for the Cosmos account {accountName} has not been found");
            }

            return connectionString;
        }
    }
}
