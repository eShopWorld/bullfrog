using System.Collections.Generic;

namespace Bullfrog.Actors.Interfaces.Models
{
    public class ScaleManagerConfiguration
    {
        public ScaleSetConfiguration ScaleSetConfiguration { get; set; }

        public List<CosmosConfiguration> CosmosConfigurations { get; set; }
    }
}
