using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace Bullfrog.Common.Cosmos
{
    public interface ICosmosThroughputClientFactory
    {
        ICosmosThroughputClient CreateClientClient(CosmosDbConfiguration cosmosConfiguration);

        Task<ValidationResult> Validate(CosmosDbConfiguration cosmosConfiguration);
    }

}
