using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Bullfrog.Actor.Interfaces.Models.Validation;

namespace Bullfrog.Actor.Interfaces.Models
{
    /// <summary>
    /// Defines the configuration of a scale group.
    /// </summary>
    public class ScaleGroupDefinition
    {
        /// <summary>
        /// The configurations of scale group's regions.
        /// </summary>
        [Required]
        [MinLength(1)]
        [ElementsHaveDistinctValues(nameof(ScaleGroupRegion.RegionName))]
        public List<ScaleGroupRegion> Regions { get; set; }
    }
}
