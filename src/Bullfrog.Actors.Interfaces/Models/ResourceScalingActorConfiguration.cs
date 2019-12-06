using System.Collections.Generic;

namespace Bullfrog.Actors.Interfaces.Models
{
    /// <summary>
    /// The configuration sent to the resource scaling agent.
    /// </summary>
    public class ResourceScalingActorConfiguration
    {
        /// <summary>
        /// Defines the managed VM scale set configuration.
        /// </summary>
        public ScaleSetConfiguration ScaleSetConfiguration { get; set; }

        /// <summary>
        /// Defines the managed Cosmos DB configuration.
        /// </summary>
        public CosmosConfiguration CosmosConfiguration { get; set; }

        /// <summary>
        /// Maps names to resource ids of automation accounts used to start runbooks
        /// </summary>
        public List<AutomationAccount> AutomationAccounts { get; set; }
    }
}
