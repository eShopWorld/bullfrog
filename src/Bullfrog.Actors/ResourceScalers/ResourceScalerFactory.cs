﻿using System.Linq;
using Bullfrog.Actors.Interfaces.Models;
using Bullfrog.Common;
using Bullfrog.Common.Cosmos;

namespace Bullfrog.Actors.ResourceScalers
{
    public class ResourceScalerFactory : IResourceScalerFactory
    {
        private readonly ICosmosThroughputClientFactory _cosmosThroughputClientFactory;
        private readonly ScaleSetScalerFactory _scaleSetScalerFactory;

        public ResourceScalerFactory(ICosmosThroughputClientFactory cosmosThroughputClientFactory,
            ScaleSetScalerFactory scaleSetScalerFactory)
        {
            _cosmosThroughputClientFactory = cosmosThroughputClientFactory;
            _scaleSetScalerFactory = scaleSetScalerFactory;
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
                    return new CosmosScaler(client, cosmosConfiguration);
                }
            }

            var scaleSetConfiguration = configuration.ScaleSetConfigurations.FirstOrDefault(x => x.Name == name);
            if (scaleSetConfiguration != null)
                return _scaleSetScalerFactory.CreateScaler(scaleSetConfiguration);

            throw new BullfrogException($"Configuration for {name} not found.");
        }
    }
}
