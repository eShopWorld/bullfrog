using System.Collections.Generic;

namespace Bullfrog.Actors.Interfaces.Models
{
    /// <summary>
    /// The configuration of the scale manager.
    /// </summary>
    public class ScaleManagerConfiguration
    {
        /// <summary>
        /// Defines the managed VM scale set configuration.
        /// </summary>
        public ScaleSetConfiguration ScaleSetConfiguration { get; set; }

        /// <summary>
        /// Defines the managed Cosmos DB configuration.
        /// </summary>
        public List<CosmosConfiguration> CosmosConfigurations { get; set; }
    }
}
