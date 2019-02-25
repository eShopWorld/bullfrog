using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace Bullfrog.Common
{
    public interface ICosmosDbHelper
    {
        Task<ValidationResult> ValidateConfiguration(CosmosDbConfiguration configuration);
    }

    public class CosmosDbConfiguration
    {
        public string AccountName { get; set; }

        public string DatabaseName { get; set; }

        public string ContainerName { get; set; }
    }
}
