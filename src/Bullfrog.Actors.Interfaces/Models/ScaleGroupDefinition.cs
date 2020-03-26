using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Runtime.Serialization;
using Bullfrog.Actors.Interfaces.Models.Validation;
using Newtonsoft.Json;

namespace Bullfrog.Actors.Interfaces.Models
{
    /// <summary>
    /// Defines the configuration of a scale group.
    /// </summary>
    [DataContract]
    public class ScaleGroupDefinition
    {
        /// <summary>
        /// The name of an special region which handles shared Cosmos databases.
        /// </summary>
        public const string SharedCosmosRegion = "$cosmos";

        private Dictionary<string, ScaleGroupRegion> _regionsByName;

        /// <summary>
        /// Regions (including the special Cosmos region) indexed by name.
        /// </summary>
        /// <param name="regionName">The region name.</param>
        /// <returns>The region definition.</returns>
        public ScaleGroupRegion this[string regionName]
        {
            get
            {
                if (_regionsByName == null)
                    _regionsByName = Regions.ToDictionary(r => r.RegionName);
                if (HasSharedCosmosDb && !_regionsByName.ContainsKey(SharedCosmosRegion))
                {
                    _regionsByName.Add(SharedCosmosRegion, new ScaleGroupRegion
                    {
                        RegionName = SharedCosmosRegion,
                        CosmosDbPrescaleLeadTime = CosmosDbPrescaleLeadTime,
                        Cosmos = Cosmos,
                        ScaleSets = new List<ScaleSetConfiguration>(),
                    });
                }

                return _regionsByName[regionName];
            }
        }

        /// <summary>
        /// The configurations of scale group's regions.
        /// </summary>
        [Required]
        [MinLength(1)]
        [ElementsHaveDistinctValues(nameof(ScaleGroupRegion.RegionName))]
        [DataMember]
        public List<ScaleGroupRegion> Regions { get; set; }

        /// <summary>
        /// The configuration of scaling of Cosmos DB databases or containers.
        /// </summary>
        [ElementsHaveDistinctValues(nameof(CosmosConfiguration.Name))]
        [DataMember]
        public List<CosmosConfiguration> Cosmos { get; set; }

        /// <summary>
        /// Cosmos DB prescale lead time.
        /// </summary>
        [ValueIs(ValueComparison.GreaterThanOrEqualTo, PropertyValue = nameof(ZeroTimeSpan))]
        [DataMember]
        public TimeSpan CosmosDbPrescaleLeadTime { get; set; }

        /// <summary>
        /// Defines how long after completion time a scale event can be purged.
        /// </summary>
        [ValueIs(ValueComparison.GreaterThanOrEqualTo, PropertyValue = nameof(ZeroTimeSpan))]
        [DataMember]
        public TimeSpan? OldEventsAge { get; set; }

        /// <summary>
        /// Checks whether shared Cosmos databases are defined.
        /// </summary>
        [JsonIgnore]
        public bool HasSharedCosmosDb =>
            Cosmos != null && Cosmos.Any();

        /// <summary>
        /// Returns names of all regions (including shared Cosmos region if it exists).
        /// </summary>
        [JsonIgnore]
        public IEnumerable<string> AllRegionNames
        {
            get
            {
                var regionNames = Regions?.Select(x => x.RegionName);
                if (HasSharedCosmosDb)
                    regionNames = regionNames?.Concat(new[] { SharedCosmosRegion });
                return regionNames;
            }
        }

        /// <summary>
        /// Returns the maximal lead time for given regions.
        /// </summary>
        /// <param name="regions">The list of regions.</param>
        /// <returns>The maximal lead time.</returns>
        public TimeSpan MaxLeadTime(IEnumerable<string> regions) =>
            regions.Select(r => this[r].MaxLeadTime)
                .Union(Enumerable.Repeat(CosmosDbPrescaleLeadTime, 1))
                .Max();

        private static TimeSpan ZeroTimeSpan => TimeSpan.Zero;
    }
}
