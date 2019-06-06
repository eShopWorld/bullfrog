using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Bullfrog.Actors.Interfaces;
using Bullfrog.Actors.Interfaces.Models;
using Bullfrog.Common;
using Eshopworld.Core;
using Microsoft.ServiceFabric.Actors;
using Microsoft.ServiceFabric.Actors.Client;
using Microsoft.ServiceFabric.Actors.Runtime;

namespace Bullfrog.Actors
{
    internal class CosmosManagementActor : BullfrogActorBase, ICosmosManagementActor, IRemindable
    {
        private readonly StateItem<int?> _newThroughputStateItem;
        private readonly StateItem<int?> _requestedThroughputStateItem;
        private readonly StateItem<CosmosConfiguration> _cosmosConfigurationStateItem;
        private readonly StateItem<ActorId> _ownerActorIdStateItem;
        private readonly IActorProxyFactory _proxyFactory;
        private ICosmosThroughputClient _cosmosThroughputClient;

        public CosmosManagementActor(ActorService actorService, ActorId actorId, IActorProxyFactory proxyFactory, IBigBrother bigBrother)
            : base(actorService, actorId, bigBrother)
        {
            _requestedThroughputStateItem = new StateItem<int?>(StateManager, "requestedThroughput");
            _cosmosConfigurationStateItem = new StateItem<CosmosConfiguration>(StateManager, "cosmosConfiguration");
            _ownerActorIdStateItem = new StateItem<ActorId>(StateManager, "ownerActorId");
            _newThroughputStateItem = new StateItem<int?>(StateManager, "newThroughput");
            _proxyFactory = proxyFactory;
        }

        async Task IRemindable.ReceiveReminderAsync(string reminderName, byte[] state, TimeSpan dueTime, TimeSpan period)
        {
            var currentThroughput = await _cosmosThroughputClient.Get();
            if (currentThroughput.IsThroughputChangePending)
            {
                await WakeUp(TimeSpan.FromSeconds(60));
                return;
            }

            var changeRequested = await _newThroughputStateItem.TryGet();
            if (changeRequested.HasValue)
            {
                var throughput = await SetNewThroughput(changeRequested.Value, currentThroughput);
                if (throughput.HasValue)
                {
                    await NotifyOwner(changeRequested.Value, throughput.Value);
                }

                return;
            }

            var requestedThroughput = await _requestedThroughputStateItem.TryGet();
            if (requestedThroughput.HasValue)
            {
                await NotifyOwner(requestedThroughput.Value, currentThroughput.Throughput);
                await _requestedThroughputStateItem.Remove();
            }
        }

        async Task<int?> ICosmosManagementActor.ResetThroughput()
        {
            return await SetNewThroughput(null);
        }

        async Task ICosmosManagementActor.SetConfiguration(CosmosConfiguration cosmosConfiguration, ActorId ownerActorId)
        {
            await _ownerActorIdStateItem.Set(ownerActorId);
            throw new NotImplementedException();
        }

        async Task<int?> ICosmosManagementActor.SetThroughput(int throughput)
        {
            return await SetNewThroughput(throughput);
        }

        private async Task<int?> SetNewThroughput(int? newThroughput, CosmosThroughput currentThroughput = null)
        {
            if (currentThroughput == null)
                currentThroughput = await _cosmosThroughputClient.Get();
            if (currentThroughput.IsThroughputChangePending)
            {
                // The previously started operation is still in progress. Memorize the latest request and try again in a while.
                await _newThroughputStateItem.Set(newThroughput);
                await WakeUp(TimeSpan.FromSeconds(60));
                return null;
            }

            await _newThroughputStateItem.TryRemove();

            var configuration = await _cosmosConfigurationStateItem.Get();
            var throughput = newThroughput ?? configuration.MinimumRU;

            if (throughput > configuration.MaximumRU)
                throughput = configuration.MaximumRU;

            if (throughput < currentThroughput.MinimalThroughput)
                throughput = currentThroughput.MinimalThroughput;

            if (throughput == currentThroughput.Throughput)
            {
                return throughput;
            }

            currentThroughput = await _cosmosThroughputClient.Set(throughput);
            if (currentThroughput.IsThroughputChangePending)
            {
                await _requestedThroughputStateItem.Set(newThroughput);
                await WakeUp(TimeSpan.FromSeconds(60));
                return null;
            }

            if (throughput != currentThroughput.Throughput)
            {
                // TODO: it should never happen. log it
            }

            return currentThroughput.Throughput;
        }

        private async Task NotifyOwner(int? requestedThroughput, int throughput)
        {
            var ownerActorId = await _ownerActorIdStateItem.Get();
            var ownerActor = _proxyFactory.CreateActorProxy<IScaleManager>(ownerActorId);
            await ownerActor.ScalingCompleted("", requestedThroughput, throughput);
        }

        private async Task WakeUp(TimeSpan? after = default)
        {
            await RegisterReminderAsync("aa", null, after ?? TimeSpan.Zero, TimeSpan.FromMilliseconds(-1));
        }
    }
}
