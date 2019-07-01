using System.ComponentModel.DataAnnotations;

namespace Bullfrog.Common.Models
{
    public class CosmosDbDataPlaneConnection
    {
        [Required]
        public string AccountName { get; set; }

        [Required]
        public string DatabaseName { get; set; }

        public string ContainerName { get; set; }
    }
}
