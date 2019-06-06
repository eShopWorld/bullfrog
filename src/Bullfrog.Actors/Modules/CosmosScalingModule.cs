using System;
using System.Threading.Tasks;
using Bullfrog.Actors.Interfaces.Models;
using Bullfrog.Common.Cosmos;

namespace Bullfrog.Actors.Modules
{
    class CosmosScalingModule : ScalingModule
    {
        private readonly TimeSpan CheckPeriod = TimeSpan.FromMinutes(1);
        private readonly TimeSpan ErrorDelay = TimeSpan.FromMinutes(5);
        private readonly StateItem<int?> _newThroughputStateItem;
        private readonly StateItem<int?> _requestedThroughputStateItem;
        private readonly CosmosConfiguration _cosmosConfiguration;
        private readonly IActorModuleHost _host;
        private readonly ICosmosThroughputClient _throughputClient;

        public CosmosScalingModule(IActorModuleHost host, ICosmosThroughputClient throughputClient, CosmosConfiguration cosmosConfiguration)
        {
            _requestedThroughputStateItem = new StateItem<int?>(host.StateManager, "requestedThroughput");
            _newThroughputStateItem = new StateItem<int?>(host.StateManager, "newThroughput");
            _host = host;
            _throughputClient = throughputClient;
            _cosmosConfiguration = cosmosConfiguration;
        }

        public override async Task ReceiveReminderAsync()
        {
            try
            {
                var currentThroughput = await _throughputClient.Get();
                if (currentThroughput.IsThroughputChangePending)
                {
                    await WakeUp(CheckPeriod);
                    return;
                }

                var changeRequested = await _newThroughputStateItem.TryGet();
                if (changeRequested.HasValue)
                {
                    var throughput = await SetNewThroughput(changeRequested.Value, currentThroughput);
                    if (throughput.HasValue)
                    {
                        NotifyOwner(throughput.Value, changeRequested.Value);
                    }

                    return;
                }

                var requestedThroughput = await _requestedThroughputStateItem.TryGet();
                if (requestedThroughput.HasValue)
                {
                    int throughput = (int)(currentThroughput.RequestsUnits / _cosmosConfiguration.RequestUnitsPerRequest);
                    NotifyOwner(throughput, requestedThroughput.Value);
                    await _requestedThroughputStateItem.Remove();
                }
            }
            catch
            {
                await WakeUp(ErrorDelay);
                throw;
            }
        }

        public override async Task<int?> ResetThroughput()
        {
            return await SetNewThroughput(null);
        }

        public override async Task<int?> SetThroughput(int throughput)
        {
            return await SetNewThroughput(throughput);
        }

        private async Task<int?> SetNewThroughput(int? newThroughput, CosmosThroughput currentThroughput = null)
        {
            if (currentThroughput == null)
                currentThroughput = await _throughputClient.Get();
            if (currentThroughput.IsThroughputChangePending)
            {
                // The previously started operation is still in progress. Memorize the latest request and try again in a while.
                await _newThroughputStateItem.Set(newThroughput);
                await WakeUp(CheckPeriod);
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
            await _newThroughputStateItem.TryRemove();
            if (currentThroughput.IsThroughputChangePending)
            {
                await _requestedThroughputStateItem.Set(newThroughput);
                await WakeUp(CheckPeriod);
                return null;
            }

            if (requestUnits != currentThroughput.RequestsUnits)
            {
                // TODO: it should never happen. log it
            }

            return (int)(currentThroughput.RequestsUnits / _cosmosConfiguration.RequestUnitsPerRequest);
        }

        private void NotifyOwner(int throughput, int? requestedThroughput)
        {
            PublishScaleChangedEvent(new ScaleChangedEventArgs(throughput, requestedThroughput));
        }

        private async Task WakeUp(TimeSpan? after = default)
        {
            await _host.RegisterReminderAsync("cosmosModule", null, after ?? TimeSpan.Zero, TimeSpan.FromMilliseconds(-1));
        }
    }
}
