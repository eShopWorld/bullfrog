using System.ComponentModel.DataAnnotations;

namespace Bullfrog.Actor.Interfaces.Models
{
    public class ScaleSetConfiguration
    {
        [Required]
        public string AutoscaleSettingsResourceId { get; set; }

        [Required]
        public string ProfileName { get; set; }

        [Range(0, 1000_000_000)]
        public int RequestsPerInstance { get; set; }

        [Range(1, 1000)]
        public int MinInstanceCount { get; set; }

        [Range(1, 1000)]
        public int DefaultInstanceCount { get; set; }
    }
}
