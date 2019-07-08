using System;
using System.Linq;
using Bullfrog.Actors.Interfaces.Models;
using Bullfrog.Common;
using Bullfrog.Common.Cosmos;
using Bullfrog.Common.Models;

namespace Bullfrog.Actors.ResourceScalers
{
    public class ResourceScalerFactory : IResourceScalerFactory
    {
        private readonly Func<ScaleSetConfiguration, ScaleSetScaler> _scaleSetScalerFactory;
        private readonly Func<CosmosDbDataPlaneConnection, ICosmosThroughputClient> _cosmosDataPlaneClientFactory;
        private readonly Func<CosmosConfiguration, ControlPlaneCosmosScaler> _controlPlaneCosmosScalerFactory;
        private readonly Func<ICosmosThroughputClient, CosmosConfiguration, CosmosScaler> _cosmosScalerFactory;

        public ResourceScalerFactory(
            Func<ScaleSetConfiguration, ScaleSetScaler> scaleSetScalerFactory,
            Func<CosmosDbDataPlaneConnection, ICosmosThroughputClient> cosmosDataPlaneClientFactory,
            Func<CosmosConfiguration, ControlPlaneCosmosScaler> controlPlaneCosmosScalerFactory,
            Func<ICosmosThroughputClient, CosmosConfiguration, CosmosScaler> cosmosScalerFactory)
        {
            _scaleSetScalerFactory = scaleSetScalerFactory;
            _cosmosDataPlaneClientFactory = cosmosDataPlaneClientFactory;
            _controlPlaneCosmosScalerFactory = controlPlaneCosmosScalerFactory;
            _cosmosScalerFactory = cosmosScalerFactory;
        }

        public ResourceScaler CreateScaler(string name, ScaleManagerConfiguration configuration)
        {
            if (configuration.CosmosConfigurations != null)
            {
                var cosmosConfiguration = configuration.CosmosConfigurations.FirstOrDefault(x => x.Name == name);
                if (cosmosConfiguration != null)
                {
                    if (cosmosConfiguration.ControlPlaneConnection != null)
                    {
                        return _controlPlaneCosmosScalerFactory(cosmosConfiguration);
                    }

                    var connectionDetails = cosmosConfiguration.DataPlaneConnection ?? new CosmosDbDataPlaneConnection
                    {
                        AccountName = cosmosConfiguration.AccountName,
                        ContainerName = cosmosConfiguration.ContainerName,
                        DatabaseName = cosmosConfiguration.DatabaseName,
                    };
                    var client = _cosmosDataPlaneClientFactory(connectionDetails);
                    return _cosmosScalerFactory(client, cosmosConfiguration);
                }
            }

            var scaleSetConfiguration = configuration.ScaleSetConfigurations.FirstOrDefault(x => x.Name == name);
            if (scaleSetConfiguration != null)
                return _scaleSetScalerFactory(scaleSetConfiguration);

            throw new BullfrogException($"Configuration for {name} not found.");
        }
    }
}
