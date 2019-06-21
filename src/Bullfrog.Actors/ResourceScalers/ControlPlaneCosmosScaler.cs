using System;
using System.Threading.Tasks;
using Bullfrog.Actors.EventModels;
using Bullfrog.Actors.Interfaces.Models;
using Bullfrog.Common.Cosmos;
using Eshopworld.Core;

namespace Bullfrog.Actors.ResourceScalers
{
    public class ControlPlaneCosmosScaler : ResourceScaler
    {
        private readonly ControlPlaneCosmosThroughputClient _throughputClient;
        private readonly CosmosConfiguration _cosmosConfiguration;
        private readonly IBigBrother _bigBrother;

        public ControlPlaneCosmosScaler(ControlPlaneCosmosThroughputClient throughputClient, CosmosConfiguration cosmosConfiguration, IBigBrother bigBrother)
        {
            _throughputClient = throughputClient;
            _cosmosConfiguration = cosmosConfiguration;
            _bigBrother = bigBrother;
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
                try
                {
                    requestUnits = await _throughputClient.Set(requestUnits);
                }
                catch (ThroughputOutOfRangeException ex) when (requestUnits < ex.MinimumThroughput)
                {
                    _bigBrother.Publish(new CosmosThroughputTooLow
                    {
                        CosmosAccunt = _cosmosConfiguration.Name,
                        ErrorMessage = ex.Message,
                        MinThroughput = ex.MinimumThroughput,
                        ThroughputRequired = requestUnits,
                    });
                    requestUnits = await _throughputClient.Set(ex.MinimumThroughput);
                }
            }

            return (int)(requestUnits / _cosmosConfiguration.RequestUnitsPerRequest);
        }
    }
}
