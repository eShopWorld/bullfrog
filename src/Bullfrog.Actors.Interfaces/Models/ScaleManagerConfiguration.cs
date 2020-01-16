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
        /// Defines the managed Cosmos DB configurations.
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

        /// <summary>
        /// Defines how long after completion time a scale event can be purged.
        /// </summary>
        public TimeSpan? OldEventsAge { get; set; }

        /// <summary>
        /// Maps names to resource ids of automation accounts used to start runbooks
        /// </summary>
        public List<AutomationAccount> AutomationAccounts { get; set; }

        /// <summary>
        /// The name of the scale group handled by the scale manager.
        /// </summary>
        public string ScaleGroup { get; set; }

        /// <summary>
        /// The name of the region handled by the scale manager.
        /// </summary>
        public string Region { get; set; }

        /// <summary>
        /// Controls whether ResouceScaler is used directly or through the resource scaling actors.
        /// </summary>
        public bool UseScalingActors { get; set; }
    }
}
