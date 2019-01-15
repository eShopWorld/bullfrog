using System.Collections.Generic;

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
        public List<ScaleGroupRegion> Regions { get; set; }
    }
}
