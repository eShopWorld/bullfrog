using System;
using System.Linq;
using Bullfrog.Actors.Interfaces.Models;
using Bullfrog.Common;
using Bullfrog.Common.Cosmos;
using Eshopworld.Core;

namespace Bullfrog.Actors.ResourceScalers
{
    public class ResourceScalerFactory : IResourceScalerFactory
    {
        private readonly ICosmosThroughputClientFactory _cosmosThroughputClientFactory;
        private readonly Func<ScaleSetConfiguration, ScaleSetScaler> _scaleSetScalerFactory;
        private readonly IBigBrother _bigBrother;

        public ResourceScalerFactory(ICosmosThroughputClientFactory cosmosThroughputClientFactory,
            Func<ScaleSetConfiguration, ScaleSetScaler> scaleSetScalerFactory,
            IBigBrother bigBrother)
        {
            _cosmosThroughputClientFactory = cosmosThroughputClientFactory;
            _scaleSetScalerFactory = scaleSetScalerFactory;
            _bigBrother = bigBrother;
        }

        public ResourceScaler CreateScaler(string name, ScaleManagerConfiguration configuration)
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
                    return new CosmosScaler(client, cosmosConfiguration, _bigBrother);
                }
            }

            var scaleSetConfiguration = configuration.ScaleSetConfigurations.FirstOrDefault(x => x.Name == name);
            if (scaleSetConfiguration != null)
                return _scaleSetScalerFactory(scaleSetConfiguration);

            throw new BullfrogException($"Configuration for {name} not found.");
        }
    }
}
