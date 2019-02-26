using System;
using System.Collections.Generic;

namespace Bullfrog.Actors.Interfaces.Models
{
    /// <summary>
    /// The configuration of the scale manager.
    /// </summary>
    public class ScaleManagerConfiguration
    {
        /// <summary>
        /// Defines the managed VM scale set configurations.
        /// </summary>
        public List<ScaleSetConfiguration> ScaleSetConfigurations { get; set; }

        /// <summary>
        /// Defines the managed Cosmos DB configuration.
        /// </summary>
        public List<CosmosConfiguration> CosmosConfigurations { get; set; }


        /// <summary>
        /// VM scale sets prescale lead time.
        /// </summary>
        public TimeSpan ScaleSetPrescaleLeadTime { get; set; }

        /// <summary>
        /// Cosmos DB prescale lead time.
        /// </summary>
        public TimeSpan CosmosDbPrescaleLeadTime { get; set; }
    }
}
