using System;
using System.Threading.Tasks;
using Bullfrog.Actors.EventModels;
using Bullfrog.Actors.Interfaces.Models;
using Bullfrog.Common.Cosmos;
using Eshopworld.Core;

namespace Bullfrog.Actors.ResourceScalers
{
    public class CosmosScaler : ResourceScaler
    {
        private readonly ICosmosThroughputClient _throughputClient;
        private readonly CosmosConfiguration _cosmosConfiguration;
        private readonly IBigBrother _bigBrother;

        public CosmosScaler(ICosmosThroughputClient throughputClient, CosmosConfiguration cosmosConfiguration, IBigBrother bigBrother)
        {
            _throughputClient = throughputClient;
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
            var currentThroughput = await _throughputClient.Get();
            if (currentThroughput.IsThroughputChangePending)
                return null;

            var newRequestUnits = (int)((throughput ?? 0) * _cosmosConfiguration.RequestUnitsPerRequest);
            var roundedRequestUnits = (newRequestUnits + 99) / 100 * 100;

            var requestUnits = Math.Max(roundedRequestUnits, _cosmosConfiguration.MinimumRU);

            if (requestUnits > _cosmosConfiguration.MaximumRU)
                requestUnits = _cosmosConfiguration.MaximumRU;

            if (requestUnits < currentThroughput.MinimalRequestUnits)
                requestUnits = currentThroughput.MinimalRequestUnits;

            _bigBrother.Publish(new CosmosThroughputReport
            {
                ScalerName = _cosmosConfiguration.Name,
                RequestedThroughput = throughput ?? -1,
                PreviousRequestUnits = currentThroughput.RequestsUnits,
                NewRequestUnits = requestUnits,
            });

            if (requestUnits != currentThroughput.RequestsUnits)
            {
                currentThroughput = await _throughputClient.Set(requestUnits);
                if (currentThroughput.IsThroughputChangePending)
                    return null;

                if (requestUnits != currentThroughput.RequestsUnits)
                {
                    _bigBrother.Publish(new ResourceScalingIssue
                    {
                        Message = $"The throughput set operation returned unexpected value {currentThroughput.RequestsUnits} when throughput was set to {requestUnits}.",
                        ResourceName = _cosmosConfiguration.Name,
                    });
                }
            }

            return (int)(currentThroughput.RequestsUnits / _cosmosConfiguration.RequestUnitsPerRequest);
        }
    }
}
