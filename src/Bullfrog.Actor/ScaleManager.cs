namespace Bullfrog.Actors
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Bullfrog.Actors.EventModels;
    using Bullfrog.Actors.Helpers;
    using Bullfrog.Actors.Interfaces;
    using Bullfrog.Actors.Interfaces.Models;
    using Bullfrog.Actors.Models;
    using Bullfrog.Common;
    using Eshopworld.Core;
    using Microsoft.ServiceFabric.Actors;
    using Microsoft.ServiceFabric.Actors.Runtime;

    [StatePersistence(StatePersistence.Persisted)]
    public class ScaleManager : Actor, IScaleManager, IRemindable
    {
        private const string ReminderName = "wakeupReminder";
        private readonly TimeSpan EstimatedScaleTime = TimeSpan.FromMinutes(5);
        private readonly StateItem<List<ManagedScaleEvent>> _events;
        private readonly StateItem<ScaleManagerConfiguration> _configuration;
        private readonly IScaleSetManager _scaleSetManager;
        private readonly ICosmosManager _cosmosManager;
        private readonly IBigBrother _bigBrother;

        /// <summary>
        /// Initializes a new instance of ScaleManager
        /// </summary>
        /// <param name="actorService">The Microsoft.ServiceFabric.Actors.Runtime.ActorService that will host this actor instance.</param>
        /// <param name="actorId">The Microsoft.ServiceFabric.Actors.ActorId for this actor instance.</param>
        public ScaleManager(
            ActorService actorService,
            ActorId actorId,
            IScaleSetManager scaleSetManager,
            ICosmosManager cosmosManager,
            IBigBrother bigBrother)
            : base(actorService, actorId)
        {
            _events = new StateItem<List<ManagedScaleEvent>>(StateManager, "scaleEvents");
            _configuration = new StateItem<ScaleManagerConfiguration>(StateManager, "configuration");
            _scaleSetManager = scaleSetManager;
            _cosmosManager = cosmosManager;
            _bigBrother = bigBrother;
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
            await _events.TryAdd(new List<ManagedScaleEvent>());
        }

        async Task<ScaleEventState> IScaleManager.DeleteScaleEvent(Guid id, CancellationToken cancellationToken)
        {
            var events = await _events.Get(cancellationToken);
            var now = DateTimeService.UtcNow;

            var eventToDelete = events.Find(e => e.Id == id);
            var state = eventToDelete?.GetState(now) ?? ScaleEventState.NotFound;
            if (eventToDelete != null)
            {
                events.Remove(eventToDelete);
                await _events.Set(events, cancellationToken);
            }
            return state;
        }

        async Task<ScheduledScaleEvent> IScaleManager.GetScaleEvent(Guid id, CancellationToken cancellationToken)
        {
            var events = await _events.Get();

            var foundEvent = events.Find(e => e.Id == id);
            if (foundEvent == null)
                return null;
            return ToScheduledScaleEvent(foundEvent);
        }

        async Task<ScaleState> IScaleManager.GetScaleSet(CancellationToken cancellationToken)
        {
            // TODO: base scale calculation of the state of controlled resources instead of on the events
            var events = await _events.Get();
            var now = DateTimeService.UtcNow;

            // TODO: include events which start before last of the executing events finishes
            var current = events.Where(e => e.GetState(now) == ScaleEventState.Executing).ToList();
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
            var events = await _events.Get();

            return events.Select(ToScheduledScaleEvent).ToList();
        }

        async Task<UpdatedScheduledScaleEvent> IScaleManager.ScheduleScaleEvent(ScaleEvent scaleEvent, CancellationToken cancellationToken)
        {
            var configuration = await _configuration.TryGet(cancellationToken);
            if (!configuration.HasValue)
            {
                // TODO: return something instead of throwing an exception
                throw new Exception($"The scale manager {Id} is not active");
            }

            var events = await _events.Get();

            var modifiedEvent = events.Find(e => e.Id == scaleEvent.Id);
            var state = modifiedEvent?.GetState(DateTimeService.UtcNow) ?? ScaleEventState.NotFound;

            if (modifiedEvent == null)
            {
                modifiedEvent = new ManagedScaleEvent { Id = scaleEvent.Id };
                events.Add(modifiedEvent);
            }

            modifiedEvent.Name = scaleEvent.Name;
            modifiedEvent.RequiredScaleAt = scaleEvent.RequiredScaleAt;
            modifiedEvent.Scale = scaleEvent.Scale;
            modifiedEvent.StartScaleDownAt = scaleEvent.StartScaleDownAt;
            modifiedEvent.EstimatedScaleUpAt = modifiedEvent.RequiredScaleAt - EstimatedScaleTime;

            await _events.Set(events, cancellationToken);

            await UpdateState();

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

        async Task IScaleManager.Disable(CancellationToken cancellationToken)
        {
            await _events.Set(new List<ManagedScaleEvent>(), cancellationToken);
            await _configuration.TryRemove();
            await WakeMeAt(null);
        }

        async Task IScaleManager.Configure(ScaleManagerConfiguration configuration, CancellationToken cancellationToken)
        {
            await _configuration.Set(configuration, cancellationToken);
            await UpdateState();
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

        async Task IRemindable.ReceiveReminderAsync(string reminderName, byte[] state, TimeSpan dueTime, TimeSpan period)
        {
            await UpdateState();
        }

        private async Task UpdateState(CancellationToken cancellationToken = default)
        {
            var configuration = await _configuration.Get(cancellationToken);
            var events = await _events.Get(cancellationToken);
            var now = DateTimeService.UtcNow;
            var expectedRequestsNumber = CalculateCurrentTotalScaleRequest(events, now);

            // TODO: can all scale operations be done concurrently (if it's too long to do it sequentially)? Is Azure Fluent safe to use in such scenario?
            var scaleSetInstances = await UpdateScaleSet(configuration.ScaleSetConfiguration, expectedRequestsNumber);
            var cosmosScaleState = await UpdateCosmosInstances(configuration.CosmosConfigurations, expectedRequestsNumber);

            var nextWakeUpTime = FindNextWakeUpTime(events, now);
            await WakeMeAt(nextWakeUpTime);

            _bigBrother.Publish(new ScaleAgentStatus
            {
                ActorId = Id.ToString(),
                RequestedScale = expectedRequestsNumber,
                NextWakeUpTime = nextWakeUpTime,
                ScaleSetInstances = scaleSetInstances,
                Cosmos = cosmosScaleState,
            });
        }

        private async Task<int> UpdateScaleSet(ScaleSetConfiguration configuration, int? expectedRequestsNumber)
        {
            int scaleSetInstances;
            try
            {
                if (expectedRequestsNumber.HasValue)
                {
                    scaleSetInstances = await _scaleSetManager.SetScale(expectedRequestsNumber.Value, configuration, default);
                }
                else
                {
                    scaleSetInstances = await _scaleSetManager.Reset(configuration, default);
                }
            }
            catch (Exception ex) // TODO: can/should it be more specific?
            {
                scaleSetInstances = -1;
                var error = new Exception($"Failed to scale {configuration.AutoscaleSettingsResourceId}.", ex);
                _bigBrother.Publish(error.ToExceptionEvent());
            }

            return scaleSetInstances;
        }

        private async Task<List<CosmosScale>> UpdateCosmosInstances(List<CosmosConfiguration> cosmosInstances, int? expectedRequestsNumber)
        {
            var cosmosScaleState = new List<CosmosScale>();
            foreach (var cosmosConfiguration in cosmosInstances)
            {
                int cosmosRUs = -1;
                try
                {
                    if (expectedRequestsNumber.HasValue)
                    {
                        cosmosRUs = await _cosmosManager.SetScale(expectedRequestsNumber.Value, cosmosConfiguration);
                    }
                    else
                    {
                        cosmosRUs = await _cosmosManager.Reset(cosmosConfiguration);
                    }
                }
                catch (Exception ex)
                {
                    var error = new Exception($"Failed to scale the {cosmosConfiguration.Name} cosmos instance.", ex);
                    _bigBrother.Publish(error.ToExceptionEvent());
                }

                cosmosScaleState.Add(new CosmosScale { Name = cosmosConfiguration.Name, RUs = cosmosRUs });
            }

            return cosmosScaleState;
        }

        protected virtual async Task WakeMeAt(DateTimeOffset? time)
        {
            TimeSpan dueTime;
            if (time.HasValue)
            {
                dueTime = time.Value - DateTimeService.UtcNow;
                if (dueTime < TimeSpan.Zero)
                {
                    dueTime = TimeSpan.Zero;
                }

                await RegisterReminderAsync(ReminderName, null, dueTime, TimeSpan.FromMilliseconds(-1));
            }
            else
            {
                //dueTime = TimeSpan.FromMilliseconds(-1); // disable reminder // Due to a bug in SDK it doesn't work
                try
                {
                    var reminder = GetReminder(ReminderName);
                    await UnregisterReminderAsync(reminder);
                }
                catch (ReminderNotFoundException)
                {
                    // It happens if the reminder is not registered. Ignore.
                }
            }
        }

        private static int? CalculateCurrentTotalScaleRequest(IEnumerable<ManagedScaleEvent> events, DateTimeOffset now)
        {
            int? total = null;
            foreach (var ev in events.Where(e => e.EstimatedScaleUpAt <= now && now < e.StartScaleDownAt))
            {
                total = checked(ev.Scale + total.GetValueOrDefault());
            }

            return total;
        }

        private static DateTimeOffset? FindNextWakeUpTime(IEnumerable<ManagedScaleEvent> events, DateTimeOffset now)
        {
            var nextTime = DateTimeOffset.MaxValue;

            foreach (var ev in events)
            {
                if (now < ev.EstimatedScaleUpAt)
                {
                    if (ev.EstimatedScaleUpAt < nextTime)
                    {
                        nextTime = ev.EstimatedScaleUpAt;
                    }
                }
                else if (now < ev.StartScaleDownAt && ev.StartScaleDownAt < nextTime)
                {
                    nextTime = ev.StartScaleDownAt;
                }
            }

            return nextTime == DateTimeOffset.MaxValue ? null : (DateTimeOffset?)nextTime;
        }
    }
}
