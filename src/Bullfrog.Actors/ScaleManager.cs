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
    using Bullfrog.DomainEvents;
    using Eshopworld.Core;
    using Microsoft.ServiceFabric.Actors;
    using Microsoft.ServiceFabric.Actors.Runtime;

    [StatePersistence(StatePersistence.Persisted)]
    public class ScaleManager : BullfrogActorBase, IScaleManager, IRemindable
    {
        private const string ReminderName = "wakeupReminder";
        private readonly StateItem<List<ManagedScaleEvent>> _events;
        private readonly StateItem<ScaleManagerConfiguration> _configuration;
        private readonly StateItem<DateTimeOffset> _scaleOutStarted;
        private readonly StateItem<Dictionary<Guid, ScaleChangeType>> _reportedEventStates;
        private readonly IScaleSetManager _scaleSetManager;
        private readonly ICosmosManager _cosmosManager;
        private readonly IScaleSetMonitor _scaleSetMonitor;
        private readonly IDateTimeProvider _dateTimeProvider;

        /// <summary>
        /// Initializes a new instance of ScaleManager
        /// </summary>
        /// <param name="actorService">The Microsoft.ServiceFabric.Actors.Runtime.ActorService that will host this actor instance.</param>
        /// <param name="actorId">The Microsoft.ServiceFabric.Actors.ActorId for this actor instance.</param>
        /// <param name="scaleSetManager">Updates the configuration of a VM scale set to handle a requested thoughput.</param>
        /// <param name="cosmosManager">Updates configuration of the Cosmos DB to handle a requested thoughput.</param>
        /// <param name="scaleSetMonitor">Reads performance details of a scale set.</param>
        /// <param name="dateTimeProvider">Gets the current time.</param>
        /// <param name="bigBrother">Performs the logging.</param>
        public ScaleManager(
            ActorService actorService,
            ActorId actorId,
            IScaleSetManager scaleSetManager,
            ICosmosManager cosmosManager,
            IScaleSetMonitor scaleSetMonitor,
            IDateTimeProvider dateTimeProvider,
            IBigBrother bigBrother)
            : base(actorService, actorId, bigBrother)
        {
            _events = new StateItem<List<ManagedScaleEvent>>(StateManager, "scaleEvents");
            _configuration = new StateItem<ScaleManagerConfiguration>(StateManager, "configuration");
            _scaleOutStarted = new StateItem<DateTimeOffset>(StateManager, "scaleOutStarted");
            _reportedEventStates = new StateItem<Dictionary<Guid, ScaleChangeType>>(StateManager, "reportedEventStates");
            _scaleSetManager = scaleSetManager;
            _cosmosManager = cosmosManager;
            _scaleSetMonitor = scaleSetMonitor;
            _dateTimeProvider = dateTimeProvider;
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

        async Task IScaleManager.DeleteScaleEvent(Guid id, CancellationToken cancellationToken)
        {
            var events = await _events.Get(cancellationToken);

            var eventToDelete = events.Find(e => e.Id == id);
            if (eventToDelete != null)
            {
                events.Remove(eventToDelete);
                await _events.Set(events, cancellationToken);
                await UpdateState();
            }
        }

        async Task<ScaleState> IScaleManager.GetScaleSet(CancellationToken cancellationToken)
        {
            var events = await _events.Get();
            var now = _dateTimeProvider.UtcNow;

            var scaleOutStarted = await _scaleOutStarted.TryGet();
            if (!scaleOutStarted.HasValue)
                return null;

            var configuration = await _configuration.TryGet(cancellationToken);
            if (!configuration.HasValue)
                return null;

            var requestedScale = events
                .Where(e => e.GetState(now) == ScaleEventState.Executing)
                .Sum(e => (int?)e.Scale);

            var scaleSetStates = await ReadScaleSetScales(configuration.Value);

            var leadTime = configuration.Value.ScaleSetPrescaleLeadTime;
            if (leadTime < configuration.Value.CosmosDbPrescaleLeadTime)
                leadTime = configuration.Value.CosmosDbPrescaleLeadTime;
            var combindedEnd = ScaleOutEnds(events, now, leadTime);

            return new ScaleState
            {
                Scale = scaleSetStates.Values.Cast<decimal?>().Min() ?? 0,
                RequestedScale = requestedScale,
                WasScaleUpAt = scaleOutStarted.Value,
                WillScaleDownAt = combindedEnd,
                ScaleSetState = scaleSetStates,
            };
        }

        /// <summary>
        /// Finds the end of of the last event in a series of overlapped events.
        /// </summary>
        private DateTimeOffset ScaleOutEnds(IEnumerable<ManagedScaleEvent> events, DateTimeOffset now, TimeSpan leadTime)
        {
            var sortedEvents = events.Select(e => (start: e.RequiredScaleAt - leadTime, end: e.StartScaleDownAt))
                .Where(x => x.end > now)
                .OrderBy(x => x.start)
                .ToList();
            if (sortedEvents.Count == 0)
                return now;

            var combindedEnd = sortedEvents[0].end;
            foreach (var (start, end) in sortedEvents)
            {
                if (start <= combindedEnd)
                {
                    if (combindedEnd < end)
                        combindedEnd = end;
                }
                else
                {
                    break;
                }
            }

            return combindedEnd;
        }

        async Task IScaleManager.ScheduleScaleEvent(RegionScaleEvent scaleEvent, CancellationToken cancellationToken)
        {
            var configuration = await _configuration.TryGet(cancellationToken);
            if (!configuration.HasValue)
            {
                throw new BullfrogException($"The scale manager {Id} is not active");
            }

            var events = await _events.Get();

            var modifiedEvent = events.Find(e => e.Id == scaleEvent.Id);
            if (modifiedEvent == null)
            {
                modifiedEvent = new ManagedScaleEvent { Id = scaleEvent.Id };
                events.Add(modifiedEvent);
            }

            modifiedEvent.Name = scaleEvent.Name;
            modifiedEvent.RequiredScaleAt = scaleEvent.RequiredScaleAt;
            modifiedEvent.Scale = scaleEvent.Scale;
            modifiedEvent.StartScaleDownAt = scaleEvent.StartScaleDownAt;
            var estimatedScaleTime = TimeSpan.FromTicks(Math.Max(configuration.Value.CosmosDbPrescaleLeadTime.Ticks,
                configuration.Value.ScaleSetPrescaleLeadTime.Ticks));
            modifiedEvent.EstimatedScaleUpAt = modifiedEvent.RequiredScaleAt - estimatedScaleTime;

            await _events.Set(events, cancellationToken);

            await UpdateState();
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

            var estimatedScaleTime = TimeSpan.FromTicks(Math.Max(configuration.CosmosDbPrescaleLeadTime.Ticks,
                configuration.ScaleSetPrescaleLeadTime.Ticks));
            var events = await _events.Get();
            foreach (var ev in events)
            {
                ev.EstimatedScaleUpAt = ev.RequiredScaleAt - estimatedScaleTime;
            }
            await _events.Set(events);

            await UpdateState();
        }

        async Task IRemindable.ReceiveReminderAsync(string reminderName, byte[] state, TimeSpan dueTime, TimeSpan period)
        {
            await UpdateState();
        }

        private async Task UpdateState(CancellationToken cancellationToken = default)
        {
            var configuration = await _configuration.Get(cancellationToken);
            var events = await _events.Get(cancellationToken);
            var now = _dateTimeProvider.UtcNow;

            // TODO: can all scale operations be done concurrently (if it's too long to do it sequentially)? Is Azure Fluent safe to use in such scenario?
            var scaleSetScale = CalculateCurrentTotalScaleRequest(events, now, configuration.ScaleSetPrescaleLeadTime);
            var scaleSetInstances = await UpdateScaleSets(configuration.ScaleSetConfigurations, scaleSetScale);
            var cosmosDbScale = CalculateCurrentTotalScaleRequest(events, now, configuration.CosmosDbPrescaleLeadTime);
            var cosmosScaleState = await UpdateCosmosInstances(configuration.CosmosConfigurations, cosmosDbScale);

            var scaleOutStarted = await _scaleOutStarted.TryGet();
            if (scaleSetScale.HasValue || cosmosDbScale.HasValue)
            {
                if (!scaleOutStarted.HasValue)
                    await _scaleOutStarted.Set(_dateTimeProvider.UtcNow);
            }
            else
            {
                if (scaleOutStarted.HasValue)
                    await _scaleOutStarted.TryRemove();
            }

            var maxLeadTime = configuration.ScaleSetPrescaleLeadTime < configuration.CosmosDbPrescaleLeadTime
                ? configuration.CosmosDbPrescaleLeadTime
                : configuration.ScaleSetPrescaleLeadTime;
            var currentScale = configuration.ScaleSetConfigurations.Any()
                ? (await ReadScaleSetScales(configuration)).Values.Max(v => (int)v)
                : CalculateCurrentTotalScaleRequest(events, now, TimeSpan.Zero) ?? 0;
            await ReportEventState(events, now, maxLeadTime, currentScale);

            var nextWakeUpTime = FindNextWakeUpTime(
                events,
                now,
                configuration.ScaleSetPrescaleLeadTime,
                configuration.CosmosDbPrescaleLeadTime);
            await WakeMeAt(nextWakeUpTime);

            BigBrother.Publish(new ScaleAgentStatus
            {
                ActorId = Id.ToString(),
                RequestedScaleSetScale = scaleSetScale,
                RequestedCosmosDbScale = cosmosDbScale,
                NextWakeUpTime = nextWakeUpTime,
                ScaleSets = scaleSetInstances,
                Cosmos = cosmosScaleState,
            });
        }

        private async Task ReportEventState(List<ManagedScaleEvent> events, DateTimeOffset now, TimeSpan leadTime, int currentScale)
        {
            var activeEvents = events.Where(e => e.IsActive(now, leadTime)).ToHashSet();
            var executingEvents = events.Where(e => e.IsActive(now, TimeSpan.Zero)).ToHashSet();
            var reportedEventStates = (await _reportedEventStates.TryGet()).Value ?? new Dictionary<Guid, ScaleChangeType>();
            var statesChanged = false;

            void ChangeState(Guid id, ScaleChangeType type)
            {
                reportedEventStates[id] = type;
                statesChanged = true;
                BigBrother.Publish(new ScaleChange
                {
                    Id = id,
                    Type = type,
                });
            }

            foreach (var ev in activeEvents.Where(e => !reportedEventStates.ContainsKey(e.Id)))
            {
                ChangeState(ev.Id, ScaleChangeType.ScaleOutStarted);
            }


            if (activeEvents.Sum(e => e.Scale) <= currentScale)
            {
                foreach (var ev in activeEvents.Where(e => reportedEventStates[e.Id] == ScaleChangeType.ScaleOutStarted))
                {
                    ChangeState(ev.Id, ScaleChangeType.ScaleOutComplete);
                }
            }
            else if (executingEvents.Sum(e => e.Scale) <= currentScale)
            {
                foreach (var ev in executingEvents.Where(e => reportedEventStates[e.Id] == ScaleChangeType.ScaleOutStarted))
                {
                    ChangeState(ev.Id, ScaleChangeType.ScaleOutComplete);
                }
            }

            foreach (var key in reportedEventStates.Keys.Except(activeEvents.Select(e => e.Id)).ToList())
            {
                ChangeState(key, ScaleChangeType.ScaleInStarted);
                ChangeState(key, ScaleChangeType.ScaleInComplete);
                reportedEventStates.Remove(key);
            }

            if (statesChanged)
                await _reportedEventStates.Set(reportedEventStates);
        }

        private async Task<List<ScaleSetScale>> UpdateScaleSets(List<ScaleSetConfiguration> configurations, int? expectedRequestsNumber)
        {
            var scales = new List<ScaleSetScale>();
            foreach (var configuration in configurations)
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
                    BigBrother.Publish(error.ToExceptionEvent());
                }

                scales.Add(new ScaleSetScale
                {
                    Name = configuration.Name ?? configuration.AutoscaleSettingsResourceId,
                    InstancesNumber = scaleSetInstances
                });
            }

            return scales;
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
                    BigBrother.Publish(error.ToExceptionEvent());
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
                dueTime = time.Value - _dateTimeProvider.UtcNow;
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

        private async Task<Dictionary<string, decimal>> ReadScaleSetScales(ScaleManagerConfiguration configuration)
        {
            var scaleSetStates = new Dictionary<string, decimal>();
            foreach (var scaleSet in configuration.ScaleSetConfigurations)
            {
                var workingInstances = await _scaleSetMonitor.GetNumberOfWorkingInstances(scaleSet.LoadBalancerResourceId, scaleSet.HealthPortPort);
                var usableInstances = Math.Max(workingInstances - scaleSet.ReservedInstances, 0);
                scaleSetStates.Add(scaleSet.Name, usableInstances * scaleSet.RequestsPerInstance);
            }

            return scaleSetStates;
        }

        private static int? CalculateCurrentTotalScaleRequest(IEnumerable<ManagedScaleEvent> events, DateTimeOffset now, TimeSpan leadTime)
        {
            int? total = null;
            foreach (var ev in events.Where(e => e.IsActive(now, leadTime)))
            {
                total = checked(ev.Scale + total.GetValueOrDefault());
            }

            return total;
        }

        private static DateTimeOffset? FindNextWakeUpTime(IEnumerable<ManagedScaleEvent> events, DateTimeOffset now, TimeSpan scaleSetLeadTime, TimeSpan cosmosDbLeadTime)
        {
            var nextTime = events.SelectMany(ev => new[] { ev.StartScaleDownAt, ev.RequiredScaleAt, ev.RequiredScaleAt - cosmosDbLeadTime, ev.RequiredScaleAt - scaleSetLeadTime })
                .Where(t => t > now)
                .Cast<DateTimeOffset?>()
                .Min();

            return nextTime;
        }
    }
}
