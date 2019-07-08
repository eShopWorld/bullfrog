using System;
using System.Threading.Tasks;
using Bullfrog.Actors.EventModels;
using Bullfrog.Actors.Interfaces.Models;
using Bullfrog.Common.Cosmos;
using Eshopworld.Core;
using Microsoft.Azure.Management.ResourceManager.Fluent;

namespace Bullfrog.Actors.ResourceScalers
{
    public class ControlPlaneCosmosScaler : ResourceScaler
    {
        private readonly IResourceManagementClient _resourceManagementClient;
        private readonly CosmosConfiguration _cosmosConfiguration;
        private readonly IBigBrother _bigBrother;

        public ControlPlaneCosmosScaler(IResourceManagementClient resourceManagementClient, CosmosConfiguration cosmosConfiguration, IBigBrother bigBrother)
        {
            if (cosmosConfiguration.ControlPlaneConnection == null)
                throw new ArgumentException("CosmosConfiguration must use ControlPlaneConnection.", nameof(cosmosConfiguration));

            _resourceManagementClient = resourceManagementClient;
            _cosmosConfiguration = cosmosConfiguration;
            _bigBrother = bigBrother;
        }

        public override async Task<bool> ScaleIn()
        {
            return await SetThroughput(null) != null;
        }

        public override async Task<int?> ScaleOut(int throughput, DateTimeOffset endsAt)
        {
            return await SetThroughput(throughput);
        }

        private async Task<int?> SetThroughput(int? throughput)
        {
            var newRequestUnits = (int)((throughput ?? 0) * _cosmosConfiguration.RequestUnitsPerRequest);
            var roundedRequestUnits = (newRequestUnits + 99) / 100 * 100;

            var requestUnits = Math.Max(roundedRequestUnits, _cosmosConfiguration.MinimumRU);

            if (requestUnits > _cosmosConfiguration.MaximumRU)
                requestUnits = _cosmosConfiguration.MaximumRU;

            var currentThroughput = await _resourceManagementClient.GetThroughput(_cosmosConfiguration.ControlPlaneConnection);
            if (currentThroughput != requestUnits)
            {
                try
                {
                    requestUnits = await _resourceManagementClient.SetThroughput(requestUnits, _cosmosConfiguration.ControlPlaneConnection);
                }
                catch (ThroughputOutOfRangeException ex) when (requestUnits < ex.MinimumThroughput)
                {
                    _bigBrother.Publish(new CosmosThroughputTooLow
                    {
                        CosmosAccount = _cosmosConfiguration.Name,
                        ErrorMessage = ex.Message,
                        MinThroughput = ex.MinimumThroughput,
                        ThroughputRequired = requestUnits,
                    });
                    requestUnits = await _resourceManagementClient.SetThroughput(ex.MinimumThroughput, _cosmosConfiguration.ControlPlaneConnection);
                }
            }

            return (int)(requestUnits / _cosmosConfiguration.RequestUnitsPerRequest);
        }
    }
}
