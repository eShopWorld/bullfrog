using System.ComponentModel.DataAnnotations;

namespace Bullfrog.Actor.Interfaces.Models
{
    /// <summary>
    /// Configuration of virtual machine scale set which is part of the scale group.
    /// </summary>
    public class ScaleSetConfiguration
    {
        /// <summary>
        /// The resource id of the autoscale settings which controls virtual machine scale set scaling.
        /// </summary>
        [Required]
        public string AutoscaleSettingsResourceId { get; set; }

        /// <summary>
        /// The name of the profile of autoscale settings which is used to control VMSS scaling.
        /// </summary>
        [Required]
        public string ProfileName { get; set; }

        /// <summary>
        /// The number of requests per VMSS instance
        /// </summary>
        [Range(0, 1000_000_000)]
        public int RequestsPerInstance { get; set; }

        /// <summary>
        /// The minimal number of instances defined in the profile.
        /// </summary>
        [Range(1, 1000)]
        public int MinInstanceCount { get; set; }

        /// <summary>
        /// The default number of instances defined in the profile.
        /// </summary>
        [Range(1, 1000)]
        public int DefaultInstanceCount { get; set; }
    }
}
