using System.Linq;
using Bullfrog.Actors.Interfaces.Models;
using Bullfrog.Common;
using Bullfrog.Common.Cosmos;

namespace Bullfrog.Actors.Modules
{
    public class ScaleModuleFactory : IScaleModuleFactory
    {
        private readonly ICosmosThroughputClientFactory _cosmosThroughputClientFactory;
        private readonly IScaleSetScalingModuleFactory _scaleSetScalingModuleFactory;

        public ScaleModuleFactory(ICosmosThroughputClientFactory cosmosThroughputClientFactory,
            IScaleSetScalingModuleFactory scaleSetScalingModuleFactory)
        {
            _cosmosThroughputClientFactory = cosmosThroughputClientFactory;
            _scaleSetScalingModuleFactory = scaleSetScalingModuleFactory;
        }

        public ScalingModule CreateModule(string name, ScaleManagerConfiguration configuration)
        {
            if (configuration.CosmosConfigurations != null)
            {
                var cosmosConfiguration = configuration.CosmosConfigurations.FirstOrDefault(x => x.Name == name);
                if (cosmosConfiguration != null)
                {
                    var client = _cosmosThroughputClientFactory.CreateClientClient(new CosmosDbConfiguration
                    {
                        AccountName = cosmosConfiguration.AccountName,
                        ContainerName = cosmosConfiguration.ContainerName,
                        DatabaseName = cosmosConfiguration.DatabaseName,
                    });
                    return new CosmosModule(client, cosmosConfiguration);
                }
            }

            var scaleSetConfiguration = configuration.ScaleSetConfigurations.FirstOrDefault(x => x.Name == name);
            if (scaleSetConfiguration != null)
            {
                return _scaleSetScalingModuleFactory.CreateModule(scaleSetConfiguration);
            }

            throw new BullfrogException($"Configuration for {name} not found.");
        }
    }
}
