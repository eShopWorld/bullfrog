using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Bullfrog.Actors.Interfaces.Models.Validation;

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
        public string RegionName { get; set; }

        /// <summary>
        /// The configuration of the virtual machine scale set's scaling.
        /// </summary>
        public ScaleSetConfiguration ScaleSet { get; set; }

        /// <summary>
        /// The configurations of scaling of VM scale sets.
        /// </summary>
        [Required]
        [ElementsHaveDistinctValues(nameof(ScaleSetConfiguration.Name))]
        public List<ScaleSetConfiguration> ScaleSets { get; set; }

        /// <summary>
        /// The configuration of scaling of Cosmos DB databases or containers.
        /// </summary>
        [Required]
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

#pragma warning disable IDE0052 // Remove unread private members (required by the validator of CosmosDbPrescaleLeadTime)
        private TimeSpan ZeroTimeSpan => TimeSpan.Zero;
#pragma warning restore IDE0052 // Remove unread private members
    }
}
