using System.Collections.Generic;

namespace Bullfrog.Actor.Interfaces.Models
{
    public class ScaleManagerConfiguration
    {
        public ScaleSetConfiguration ScaleSetConfiguration { get; set; }

        public List<CosmosConfiguration> CosmosConfigurations { get; set; }
    }
}
