using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace Bullfrog.Common.Cosmos
{
    internal class CosmosThroughputClientFactory : ICosmosThroughputClientFactory
    {
        public async Task<ValidationResult> Validate(CosmosDbConfiguration cosmosConfiguration)
        {
            ICosmosThroughputClientInitializer client;

            client.UseConfiguration(cosmosConfiguration);
            return await client.ValidateConfiguration();
        }

        public ICosmosThroughputClient CreateClientClient(CosmosDbConfiguration cosmosConfiguration)
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

}
