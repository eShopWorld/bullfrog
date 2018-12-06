namespace Bullfrog.Actor
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Bullfrog.Actor.Interfaces;
    using Bullfrog.Actor.Interfaces.Models;
    using Microsoft.ServiceFabric.Actors;
    using Microsoft.ServiceFabric.Actors.Runtime;

    [StatePersistence(StatePersistence.Persisted)]
    internal class ScaleManager : Actor, IScaleManager
    {
        /// <summary>
        /// Initializes a new instance of ScaleManager
        /// </summary>
        /// <param name="actorService">The Microsoft.ServiceFabric.Actors.Runtime.ActorService that will host this actor instance.</param>
        /// <param name="actorId">The Microsoft.ServiceFabric.Actors.ActorId for this actor instance.</param>
        public ScaleManager(ActorService actorService, ActorId actorId)
            : base(actorService, actorId)
        {
        }

        /// <summary>
        /// This method is called whenever an actor is activated.
        /// An actor is activated the first time any of its methods are invoked.
        /// </summary>
        protected override Task OnActivateAsync()
        {
            ActorEventSource.Current.ActorMessage(this, "Actor activated.");

            // The StateManager is this actor's private state store.
            // Data stored in the StateManager will be replicated for high-availability for actors that use volatile or persisted state storage.
            // Any serializable object can be saved in the StateManager.
            // For more information, see https://aka.ms/servicefabricactorsstateserialization

            return StateManager.TryAddStateAsync("count", 0);
        }

        Task<ScaleEventState> IScaleManager.DeleteScaleEvent(Guid id, CancellationToken cancellationToken)
        {
            //return StateManager.GetStateAsync<ScaleEventState>("count", cancellationToken);
            throw new NotImplementedException();
        }

        Task<ScheduledScaleEvent> IScaleManager.GetScaleEvent(Guid id, CancellationToken cancellationToken)
        {
            //return StateManager.GetStateAsync<ScaleEventState>("count", cancellationToken);
            throw new NotImplementedException();
        }

        Task<ScaleState> IScaleManager.GetScaleSet()
        {
            throw new NotImplementedException();
        }

        Task<List<ScheduledScaleEvent>> IScaleManager.ListScaleEvents(CancellationToken cancellationToken)
        {
            //return StateManager.GetStateAsync<ScaleEventState>("count", cancellationToken);
            throw new NotImplementedException();
        }

        Task<ScheduledScaleEvent> IScaleManager.ScheduleScaleEvent(ScaleEvent scaleEvent, CancellationToken cancellationToken)
        {
            //return StateManager.AddOrUpdateStateAsync("count", scaleEvent.Id, (key, value) => count > value ? count : value, cancellationToken);
            throw new NotImplementedException();
        }
    }
}
