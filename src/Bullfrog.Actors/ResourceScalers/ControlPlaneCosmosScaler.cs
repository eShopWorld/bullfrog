using System;
using System.Threading.Tasks;
using Bullfrog.Actors.Interfaces.Models;
using Bullfrog.Common.Cosmos;

namespace Bullfrog.Actors.ResourceScalers
{
    class ControlPlaneCosmosScaler : ResourceScaler
    {
        private readonly ControlPlaneCosmosThroughputClient _throughputClient;
        private readonly CosmosConfiguration _cosmosConfiguration;

        public ControlPlaneCosmosScaler(ControlPlaneCosmosThroughputClient throughputClient, CosmosConfiguration cosmosConfiguration)
        {
            _throughputClient = throughputClient;
            _cosmosConfiguration = cosmosConfiguration;
        }

        public override async Task<int?> SetThroughput(int? newThroughput)
        {
            var newRequestUnits = (int)((newThroughput ?? 0) * _cosmosConfiguration.RequestUnitsPerRequest);
            var roundedRequestUnits = (newRequestUnits + 99) / 100 * 100;

            var requestUnits = Math.Max(roundedRequestUnits, _cosmosConfiguration.MinimumRU);

            if (requestUnits > _cosmosConfiguration.MaximumRU)
                requestUnits = _cosmosConfiguration.MaximumRU;

            var currentThroughput = await _throughputClient.Get();
            if (currentThroughput != requestUnits)
            {
                await _throughputClient.Set(requestUnits);
                return null;
            }
            else
            {
                return (int)(requestUnits / _cosmosConfiguration.RequestUnitsPerRequest);
            }
        }
    }
}
