using System;
using System.Threading.Tasks;
using Bullfrog.Actors.Interfaces.Models;
using Bullfrog.Common.Cosmos;

namespace Bullfrog.Actors.Modules
{
    internal class CosmosModule : ScalingModule
    {
        private readonly ICosmosThroughputClient _throughputClient;
        private readonly CosmosConfiguration _cosmosConfiguration;

        public CosmosModule(ICosmosThroughputClient throughputClient, CosmosConfiguration cosmosConfiguration)
        {
            _throughputClient = throughputClient;
            _cosmosConfiguration = cosmosConfiguration;
        }

        public override async Task<int?> SetThroughput(int? newThroughput)
        {
            var currentThroughput = await _throughputClient.Get();
            if (currentThroughput.IsThroughputChangePending)
            {
                return null;
            }

            var newRequestUnits = (int)((newThroughput ?? 0) * _cosmosConfiguration.RequestUnitsPerRequest);
            var roundedRequestUnits = (newRequestUnits + 99) / 100 * 100;

            var requestUnits = Math.Max(roundedRequestUnits, _cosmosConfiguration.MinimumRU);

            if (requestUnits > _cosmosConfiguration.MaximumRU)
                requestUnits = _cosmosConfiguration.MaximumRU;

            if (requestUnits < currentThroughput.MinimalRequestUnits)
                requestUnits = currentThroughput.MinimalRequestUnits;

            if (requestUnits == currentThroughput.RequestsUnits)
            {
                return requestUnits;
            }

            currentThroughput = await _throughputClient.Set(requestUnits);
            if (currentThroughput.IsThroughputChangePending)
            {
                return null;
            }

            if (requestUnits != currentThroughput.RequestsUnits)
            {
                // TODO: it should never happen. log it
            }

            return (int)(currentThroughput.RequestsUnits / _cosmosConfiguration.RequestUnitsPerRequest);
        }
    }
}
