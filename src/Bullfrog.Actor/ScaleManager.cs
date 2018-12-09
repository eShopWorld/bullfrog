namespace Bullfrog.Actor
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Bullfrog.Actor.Interfaces;
    using Bullfrog.Actor.Interfaces.Models;
    using Bullfrog.Actor.Models;
    using Bullfrog.Common;
    using Microsoft.ServiceFabric.Actors;
    using Microsoft.ServiceFabric.Actors.Runtime;

    [StatePersistence(StatePersistence.Persisted)]
    public class ScaleManager : Actor, IScaleManager
    {
        private const string Events = "events";
        private readonly TimeSpan EstimatedScaleTime = TimeSpan.FromMinutes(new Random().Next(1, 10));

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
        protected override async Task OnActivateAsync()
        {
            ActorEventSource.Current.ActorMessage(this, "Actor activated.");

            // The StateManager is this actor's private state store.
            // Data stored in the StateManager will be replicated for high-availability for actors that use volatile or persisted state storage.
            // Any serializable object can be saved in the StateManager.
            // For more information, see https://aka.ms/servicefabricactorsstateserialization

            // For simplicity store all events in one list. Consider creating Future and Completed set of scale events for performance improvements.

            try
            {
                await StateManager.TryGetStateAsync<List<ManagedScaleEvent>>(Events);
            }
            catch
            {
                await StateManager.RemoveStateAsync(Events);
            }

            await StateManager.TryAddStateAsync(Events, new List<ManagedScaleEvent>());
        }

        async Task<ScaleEventState> IScaleManager.DeleteScaleEvent(Guid id, CancellationToken cancellationToken)
        {
            var events = await StateManager.GetStateAsync<List<ManagedScaleEvent>>(Events);

            var eventToDelete = events.Find(e => e.Id == id);
            var state = eventToDelete?.State ?? ScaleEventState.NotFound;
            if (eventToDelete != null)
            {
                events.Remove(eventToDelete);
                await StateManager.SetStateAsync(Events, events, cancellationToken);
            }
            return state;
        }

        async Task<ScheduledScaleEvent> IScaleManager.GetScaleEvent(Guid id, CancellationToken cancellationToken)
        {
            var events = await StateManager.GetStateAsync<List<ManagedScaleEvent>>(Events);

            var foundEvent = events.Find(e => e.Id == id);
            if (foundEvent == null)
                return null;
            return ToScheduledScaleEvent(foundEvent);
        }

        async Task<ScaleState> IScaleManager.GetScaleSet(CancellationToken cancellationToken)
        {
            var events = await StateManager.GetStateAsync<List<ManagedScaleEvent>>(Events);

            var now = DateTimeService.UtcNow;
            // TODO: include events which start before last of the executing events finishes
            var current = events.Where(e => e.State == ScaleEventState.Executing).ToList();
            if (current.Count == 0)
            {
                return null;
            }

            return new ScaleState
            {
                Scale = current.Sum(e => e.Scale),
                WasScaleUpAt = current.Min(e => e.RequiredScaleAt),
                WillScaleDownAt = current.Max(e => e.StartScaleDownAt),
            };
        }

        async Task<List<ScheduledScaleEvent>> IScaleManager.ListScaleEvents(CancellationToken cancellationToken)
        {
            var events = await StateManager.GetStateAsync<List<ManagedScaleEvent>>(Events);

            return events.Select(ToScheduledScaleEvent).ToList();
        }

        async Task<UpdatedScheduledScaleEvent> IScaleManager.ScheduleScaleEvent(ScaleEvent scaleEvent, CancellationToken cancellationToken)
        {
            var events = await StateManager.GetStateAsync<List<ManagedScaleEvent>>(Events);

            var modifiedEvent = events.Find(e => e.Id == scaleEvent.Id);
            var state = modifiedEvent?.State ?? ScaleEventState.NotFound;

            if (modifiedEvent == null)
                modifiedEvent = new ManagedScaleEvent { Id = scaleEvent.Id };
            modifiedEvent.Name = scaleEvent.Name;
            modifiedEvent.RequiredScaleAt = scaleEvent.RequiredScaleAt;
            modifiedEvent.Scale = scaleEvent.Scale;
            modifiedEvent.StartScaleDownAt = scaleEvent.StartScaleDownAt;
            modifiedEvent.EstimatedScaleUpAt = modifiedEvent.RequiredScaleAt - EstimatedScaleTime;
            events.Add(modifiedEvent);

            await StateManager.SetStateAsync(Events, events, cancellationToken);

            return new UpdatedScheduledScaleEvent
            {
                PreState = state,
                Id = modifiedEvent.Id,
                EstimatedScaleUpAt = modifiedEvent.EstimatedScaleUpAt,
                Name = modifiedEvent.Name,
                RequiredScaleAt = modifiedEvent.RequiredScaleAt,
                Scale = modifiedEvent.Scale,
                StartScaleDownAt = modifiedEvent.StartScaleDownAt,
            };
        }

        private static ScheduledScaleEvent ToScheduledScaleEvent(ManagedScaleEvent foundEvent)
        {
            return new ScheduledScaleEvent
            {
                EstimatedScaleUpAt = foundEvent.EstimatedScaleUpAt,
                Id = foundEvent.Id,
                Name = foundEvent.Name,
                RequiredScaleAt = foundEvent.RequiredScaleAt,
                Scale = foundEvent.Scale,
                StartScaleDownAt = foundEvent.StartScaleDownAt,
            };
        }
    }
}
