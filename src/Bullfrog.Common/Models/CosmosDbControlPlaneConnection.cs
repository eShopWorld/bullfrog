using System.ComponentModel.DataAnnotations;
using Bullfrog.Common.Models.Validation;

namespace Bullfrog.Common.Models
{
    public class CosmosDbControlPlaneConnection
    {
        [Required]
        [AzureResourceId]
        public string AccountResurceId { get; set; }

        [Required]
        public string DatabaseName { get; set; }

        public string ContainerName { get; set; }
    }
}
