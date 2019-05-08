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
        private Dictionary<string, ScaleGroupRegion> _regionsByName;

        internal ScaleGroupRegion this[string regionName]
        {
            get
            {
                if (_regionsByName == null)
                    _regionsByName = Regions.ToDictionary(r => r.RegionName);

                return _regionsByName[regionName];
            }
        }

        /// <summary>
        /// The configurations of scale group's regions.
        /// </summary>
        [Required]
        [MinLength(1)]
        [ElementsHaveDistinctValues(nameof(ScaleGroupRegion.RegionName))]
        public List<ScaleGroupRegion> Regions { get; set; }

        /// <summary>
        /// The configuration of scaling of Cosmos DB databases or containers.
        /// </summary>
        [ElementsHaveDistinctValues(nameof(CosmosConfiguration.Name))]
        public List<CosmosConfiguration> Cosmos { get; set; }

        /// <summary>
        /// Cosmos DB prescale lead time.
        /// </summary>
        [ValueIs(ValueComparison.GreaterThanOrEqualTo, PropertyValue = nameof(ZeroTimeSpan))]
        public TimeSpan CosmosDbPrescaleLeadTime { get; set; }

        /// <summary>
        /// Checks whether shared Cosmos databases are defined.
        /// </summary>
        [JsonIgnore]
        public bool HasSharedCosmosDb =>
            Cosmos != null && Cosmos.Any();

        /// <summary>
        /// Returns the maximal lead time for given regions.
        /// </summary>
        /// <param name="regions">The list of regions.</param>
        /// <returns>The maximal lead time.</returns>
        public TimeSpan MaxLeadTime(IEnumerable<string> regions) =>
            regions.Select(r => this[r].MaxLeadTime)
                .Union(Enumerable.Repeat(CosmosDbPrescaleLeadTime, 1))
                .Max();

        private TimeSpan ZeroTimeSpan => TimeSpan.Zero;
    }
}
