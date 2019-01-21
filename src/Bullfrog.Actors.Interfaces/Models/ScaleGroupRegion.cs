﻿using System.Collections.Generic;
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
        [Required]
        public ScaleSetConfiguration ScaleSet { get; set; }

        /// <summary>
        /// The configuration of scaling of Cosmos DB databases or containers.
        /// </summary>
        [Required]
        [ElementsHaveDistinctValues(nameof(CosmosConfiguration.Name))]
        public List<CosmosConfiguration> Cosmos { get; set; }
    }
}