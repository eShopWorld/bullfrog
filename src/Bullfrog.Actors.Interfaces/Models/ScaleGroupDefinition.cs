using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Bullfrog.Actors.Interfaces.Models.Validation;
using Newtonsoft.Json;

namespace Bullfrog.Actors.Interfaces.Models
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

        /// <summary>
        /// Returns the maximal lead time used by any of the regions.
        /// </summary>
        [JsonIgnore]
        public TimeSpan MaxLeadTime => Regions?.Count > 0
            ? Regions.Select(r => r.ScaleSetPrescaleLeadTime)
            .Union(Regions.Select(r => r.CosmosDbPrescaleLeadTime))
            .Max()
            : TimeSpan.Zero;
    }
}
