using System.ComponentModel.DataAnnotations;

namespace Bullfrog.Actor.Interfaces.Models
{
    /// <summary>
    /// The scale group's region configuration.
    /// </summary>
    public class ScaleGroupRegion
    {
        /// <summary>
        /// The name of the region.
        /// </summary>
        [Required]
        public string RegionName { get; set; }

        /// <summary>
        /// The configuration of the virtual machine scale set's scaling.
        /// </summary>
        [Required]
        public ScaleSetConfiguration ScaleSet { get; set; }
    }
}
