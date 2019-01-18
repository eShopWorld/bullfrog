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
        /// <returns>The connection string or null.</returns>
        public static string GetCosmosAccountConnectionString(this IConfigurationRoot configuration, string accountName)
        {
            var cosmosConnectionStringsSection = configuration.GetSection("Bullfrog").GetSection("Cosmos");
            return cosmosConnectionStringsSection[accountName];
        }
    }
}
