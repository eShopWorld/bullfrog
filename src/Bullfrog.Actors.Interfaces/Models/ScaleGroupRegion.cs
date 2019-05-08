﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Bullfrog.Actors.Interfaces.Models.Validation;
using Newtonsoft.Json;

namespace Bullfrog.Actors.Interfaces.Models
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
        // language=regex
        [RegularExpression(@"^[\d\w\s-]*$")]
        public string RegionName { get; set; }

        /// <summary>
        /// The configurations of scaling of VM scale sets.
        /// </summary>
        [Required]
        [ElementsHaveDistinctValues(nameof(ScaleSetConfiguration.Name))]
        public List<ScaleSetConfiguration> ScaleSets { get; set; }

        /// <summary>
        /// The configuration of scaling of Cosmos DB databases or containers.
        /// </summary>
        [ElementsHaveDistinctValues(nameof(CosmosConfiguration.Name))]
        public List<CosmosConfiguration> Cosmos { get; set; }

        /// <summary>
        /// VM scale sets prescale lead time.
        /// </summary>
        [ValueIs(ValueComparison.GreaterThanOrEqualTo, PropertyValue = nameof(ZeroTimeSpan))]
        public TimeSpan ScaleSetPrescaleLeadTime { get; set; }

        /// <summary>
        /// Cosmos DB prescale lead time.
        /// </summary>
        [ValueIs(ValueComparison.GreaterThanOrEqualTo, PropertyValue = nameof(ZeroTimeSpan))]
        public TimeSpan CosmosDbPrescaleLeadTime { get; set; }

        /// <summary>
        /// The maximal lead time for this region
        /// </summary>
        [JsonIgnore]
        public TimeSpan MaxLeadTime => CosmosDbPrescaleLeadTime < ScaleSetPrescaleLeadTime
            ? ScaleSetPrescaleLeadTime
            : CosmosDbPrescaleLeadTime;

        private TimeSpan ZeroTimeSpan => TimeSpan.Zero;
    }
}
