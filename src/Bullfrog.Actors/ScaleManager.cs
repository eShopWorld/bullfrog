namespace Bullfrog.Actors
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using Bullfrog.Actors.EventModels;
    using Bullfrog.Actors.Helpers;
    using Bullfrog.Actors.Interfaces;
    using Bullfrog.Actors.Interfaces.Models;
    using Bullfrog.Actors.Models;
    using Bullfrog.Actors.Modules;
    using Bullfrog.Common;
    using Bullfrog.Common.Cosmos;
    using Bullfrog.DomainEvents;
    using Eshopworld.Core;
    using Microsoft.ServiceFabric.Actors;
    using Microsoft.ServiceFabric.Actors.Client;
    using Microsoft.ServiceFabric.Actors.Runtime;

    [StatePersistence(StatePersistence.Persisted)]
    public class ScaleManager : BullfrogActorBase, IScaleManager, IRemindable
    {
        private const string ReminderName = "wakeupReminder";
        private readonly TimeSpan ScanPeriod = TimeSpan.FromMinutes(2);
        private readonly StateItem<List<ManagedScaleEvent>> _events;
        private readonly StateItem<ScaleManagerConfiguration> _configuration;
        private readonly StateItem<DateTimeOffset> _scaleOutStarted;
        private readonly StateItem<Dictionary<Guid, ScaleChangeType>> _reportedEventStates;
        private readonly IScaleModuleFactory _scaleModuleFactory;
        private readonly IScaleSetMonitor _scaleSetMonitor;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly IActorProxyFactory _proxyFactory;
        private readonly string _scaleGroupName;
        private readonly string _regionName;
        private readonly Dictionary<string, Modules.ScalingModule> _modules
            = new Dictionary<string, Modules.ScalingModule>();

        /// <summary>
        /// Initializes a new instance of ScaleManager
        /// </summary>
        /// <param name="actorService">The Microsoft.ServiceFabric.Actors.Runtime.ActorService that will host this actor instance.</param>
        /// <param name="actorId">The Microsoft.ServiceFabric.Actors.ActorId for this actor instance.</param>
        /// <param name="scaleModuleFactory">Scale module factory.</param>
        /// <param name="scaleSetMonitor">Reads performance details of a scale set.</param>
        /// <param name="dateTimeProvider">Gets the current time.</param>
        /// <param name="proxyFactory">Actor proxy factory.</param>
        /// <param name="bigBrother">Performs the logging.</param>
        public ScaleManager(
            ActorService actorService,
            ActorId actorId,
            IScaleModuleFactory scaleModuleFactory,
            IScaleSetMonitor scaleSetMonitor,
            IDateTimeProvider dateTimeProvider,
            IActorProxyFactory proxyFactory,
            IBigBrother bigBrother)
            : base(actorService, actorId, bigBrother)
        {
            _events = new StateItem<List<ManagedScaleEvent>>(StateManager, "scaleEvents");
            _configuration = new StateItem<ScaleManagerConfiguration>(StateManager, "configuration");
            _scaleOutStarted = new StateItem<DateTimeOffset>(StateManager, "scaleOutStarted");
            _reportedEventStates = new StateItem<Dictionary<Guid, ScaleChangeType>>(StateManager, "reportedEventStates");
            _scaleModuleFactory = scaleModuleFactory;
            _scaleSetMonitor = scaleSetMonitor;
            _dateTimeProvider = dateTimeProvider;
            _proxyFactory = proxyFactory;
            var match = Regex.Match(actorId.ToString(), ":(.+)/(.+)$");
            if (!match.Success)
                throw new BullfrogException($"The ActorId {actorId} has invalid format.");
            _scaleGroupName = match.Groups[1].Value;
            _regionName = match.Groups[2].Value;
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

        async Task IScaleManager.DeleteScaleEvent(Guid id)
        {
            var events = await _events.Get();

            var eventToDelete = events.Find(e => e.Id == id);
            if (eventToDelete != null)
            {
                events.Remove(eventToDelete);
                await _events.Set(events);
                await ScheduleStateUpdate();
            }
        }

        async Task<ScaleState> IScaleManager.GetScaleSet()
        {
            var events = await _events.Get();
            var now = _dateTimeProvider.UtcNow;

            var scaleOutStarted = await _scaleOutStarted.TryGet();
            if (!scaleOutStarted.HasValue)
                return null;

            var configuration = await _configuration.TryGet();
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

        async Task IScaleManager.ScheduleScaleEvent(RegionScaleEvent scaleEvent)
        {
            var configuration = await _configuration.TryGet();
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

            await _events.Set(events);

            await ScheduleStateUpdate();
        }

        async Task IScaleManager.Disable()
        {
            await _events.Set(new List<ManagedScaleEvent>());
            await _configuration.TryRemove();
            await WakeMeAt(null);
        }

        async Task IScaleManager.Configure(ScaleManagerConfiguration configuration)
        {
            await _configuration.Set(configuration);

            var estimatedScaleTime = TimeSpan.FromTicks(Math.Max(configuration.CosmosDbPrescaleLeadTime.Ticks,
                configuration.ScaleSetPrescaleLeadTime.Ticks));
            var events = await _events.Get();
            foreach (var ev in events)
            {
                ev.EstimatedScaleUpAt = ev.RequiredScaleAt - estimatedScaleTime;
            }
            await _events.Set(events);

            await ScheduleStateUpdate();
        }

        async Task IRemindable.ReceiveReminderAsync(string reminderName, byte[] state, TimeSpan dueTime, TimeSpan period)
        {
            await UpdateState();
        }

        private Task ScheduleStateUpdate()
        {
            return WakeMeAt(_dateTimeProvider.UtcNow);
        }

        void CreateScaleRequests(Dictionary<string, ScaleRequest> scaleRequests, int? throughput, string resourceType, IEnumerable<string> names)
        {
            foreach (var name in names)
            {
                scaleRequests[name]
                    = new ScaleRequest(throughput, _dateTimeProvider.UtcNow);
            }

            BigBrother.Publish(new ScaleChangeStarted
            {
                ResourceType = resourceType,
                RequestedThroughput = throughput ?? -1,
                PreviousChangeNotCompleted = scaleRequests.Any(),
            });
        }

        private async Task UpdateState(CancellationToken cancellationToken = default)
        {
            var configuration = await _configuration.Get(cancellationToken);
            var events = await _events.Get(cancellationToken);
            var now = _dateTimeProvider.UtcNow;

            bool scaleEventInProgress = false;

            int? lastScaleSetThroughputRequested = null;
            int? lastCosmosDbThroughputRequested = null;
            var scaleRequests = new Dictionary<string, ScaleRequest>();

            // TODO: replace two following ifs with one shared method

            // Check whether scale sets throughput needs to be changed.
            if (configuration.ScaleSetConfigurations.Any())
            {
                var scaleSetThroughput = CalculateTotalThroughput(events, now, configuration.ScaleSetPrescaleLeadTime);
                scaleEventInProgress |= scaleSetThroughput.HasValue;
                if (scaleSetThroughput != lastScaleSetThroughputRequested)
                {
                    // the requested throughput has been changes so scale requests for all resources must be created.
                    lastScaleSetThroughputRequested = scaleSetThroughput;
                    CreateScaleRequests(scaleRequests, scaleSetThroughput, "scalesets", configuration.ScaleSetConfigurations.Select(o => o.Name));
                }
            }

            // Check whether cosmos db throughput needs to be changed.
            if (configuration.CosmosConfigurations != null && configuration.CosmosConfigurations.Any())
            {
                var cosmosThroughput = CalculateTotalThroughput(events, now, configuration.CosmosDbPrescaleLeadTime);
                scaleEventInProgress |= cosmosThroughput.HasValue;
                if (cosmosThroughput != lastCosmosDbThroughputRequested)
                {
                    // the requested throughput has been changes so scale requests for all resources must be created.
                    lastCosmosDbThroughputRequested = cosmosThroughput;
                    CreateScaleRequests(scaleRequests, cosmosThroughput, "cosmosdb", configuration.CosmosConfigurations.Select(o => o.Name));
                }
            }

            // Process new or previously created scale change requests
            foreach (var scaleRequest in scaleRequests)
            {
                // TODO: move most of this logic to ScaleRequest

                var name = scaleRequest.Key;
                var scaleRequestDetails = scaleRequest.Value;

                if (!scaleRequestDetails.IsExecuting)
                    continue;

                if (_dateTimeProvider.UtcNow < scaleRequestDetails.TryAfter)
                    continue;

                var requestedThroughput = scaleRequestDetails.RequestedThroughput;
                var module = GetScalingModule(name, configuration);
                try
                {
                    scaleRequestDetails.FinalThroughput = await module.SetThroughput(requestedThroughput);
                    scaleRequestDetails.ResetError();
                    var status = scaleRequestDetails.Status;
                    if (status == ScaleRequestStatus.Completed || status == ScaleRequestStatus.Limited)
                    {
                        BigBrother.Publish(new ResourceScalingCompleted
                        {
                            FinalThroughput = scaleRequestDetails.FinalThroughput.Value,
                            Duration = _dateTimeProvider.UtcNow - scaleRequestDetails.OperationStarted,
                            RequiredThroughput = requestedThroughput,
                            ResourceName = name,
                        });
                    }
                }
                catch (Exception ex)
                {
                    scaleRequestDetails.RegisterError(_dateTimeProvider.UtcNow);
                    var contextEx = new BullfrogException($"Failed to scale {name} to {requestedThroughput}", ex);
                    BigBrother.Publish(contextEx);
                }
            }

            var scaleOutStarted = await _scaleOutStarted.TryGet();
            if (scaleEventInProgress)
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

            await ReportEventState(events, now, maxLeadTime,
                scaleRequests.Max(o => o.Value.FinalThroughput),
                scaleRequests.Any(o => o.Value.Status == ScaleRequestStatus.Failing));

            var nextWakeUpTime = scaleRequests.Any(o => o.Value.IsExecuting)
                ? _dateTimeProvider.UtcNow.Add(ScanPeriod)
                : FindNextWakeUpTime(
                    events,
                    now,
                    configuration.ScaleSetPrescaleLeadTime,
                    configuration.CosmosDbPrescaleLeadTime);
            await WakeMeAt(nextWakeUpTime);

            var states = scaleRequests.Values.Select(o => o.Status).ToList();
            BigBrother.Publish(new ScaleAgentStatus
            {
                ActorId = Id.ToString(),
                RequestedScaleSetScale = lastScaleSetThroughputRequested,
                RequestedCosmosDbScale = lastCosmosDbThroughputRequested,
                NextWakeUpTime = nextWakeUpTime,
                ScaleCompleted = states.Count(o => o == ScaleRequestStatus.Completed),
                ScaleFailing = states.Count(o => o == ScaleRequestStatus.Failing),
                ScaleInProgress = states.Count(o => o == ScaleRequestStatus.InProgress),
                ScaleLimited = states.Count(o => o == ScaleRequestStatus.Limited),
            });
        }

        private async Task ReportEventState(List<ManagedScaleEvent> events, DateTimeOffset now, TimeSpan leadTime, int? finalScale, bool issuesReported)
        {
            var activeEvents = events.Where(e => e.IsActive(now, leadTime)).ToHashSet();
            var executingEvents = events.Where(e => e.IsActive(now, TimeSpan.Zero)).ToHashSet();
            var reportedEventStates = (await _reportedEventStates.TryGet()).Value ?? new Dictionary<Guid, ScaleChangeType>();
            var changes = new List<(Guid id, ScaleChangeType type)>();

            void ChangeState(Guid id, ScaleChangeType type, bool temporaryIssue = false)
            {
                reportedEventStates[id] = type;
                changes.Add((id, type));
                string details = null;
                if (type == ScaleChangeType.ScaleIssue)
                    details = temporaryIssue ? "temporary" : "permanent";
                BigBrother.Publish(new EventRegionScaleChange
                {
                    Id = id,
                    RegionName = _regionName,
                    ScaleGroup = _scaleGroupName,
                    Type = type.ToString(),
                    Details = details,
                });
            }

            void HandleScaleOutCompleted(IEnumerable<ManagedScaleEvent> eventsGroup)
            {
                var scaleCompletedState = eventsGroup.Sum(e => e.Scale) <= finalScale
                    ? ScaleChangeType.ScaleOutComplete
                    : ScaleChangeType.ScaleIssue;

                foreach (var ev in eventsGroup.Where(e => reportedEventStates[e.Id] == ScaleChangeType.ScaleOutStarted))
                {
                    ChangeState(ev.Id, scaleCompletedState, temporaryIssue: false);
                }
            }

            foreach (var ev in activeEvents.Where(e => !reportedEventStates.ContainsKey(e.Id)))
            {
                ChangeState(ev.Id, ScaleChangeType.ScaleOutStarted);
            }

            if (issuesReported)
            {
                foreach (var ev in reportedEventStates.Where(e => e.Value == ScaleChangeType.ScaleOutStarted))
                    ChangeState(ev.Key, ScaleChangeType.ScaleIssue, temporaryIssue: true);
            }
            else if (finalScale.HasValue)
            {
                HandleScaleOutCompleted(executingEvents);
                HandleScaleOutCompleted(activeEvents);
            }
            else
            {
                foreach (var ev in reportedEventStates.Where(e => e.Value == ScaleChangeType.ScaleIssue))
                    ChangeState(ev.Key, ScaleChangeType.ScaleOutStarted);
            }

            foreach (var key in reportedEventStates.Keys.Except(activeEvents.Select(e => e.Id)).ToList())
            {
                ChangeState(key, ScaleChangeType.ScaleInStarted);
                ChangeState(key, ScaleChangeType.ScaleInComplete);
                reportedEventStates.Remove(key);
            }

            if (changes.Any())
            {
                var reportedChanges = changes.Select(c => new ScaleEventStateChange { EventId = c.id, State = c.type }).ToList();
                await GetConfigurationManager().ReportScaleEventState(_scaleGroupName, _regionName, reportedChanges);
                await _reportedEventStates.Set(reportedEventStates);
            }
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

        private static int? CalculateTotalThroughput(IEnumerable<ManagedScaleEvent> events, DateTimeOffset now, TimeSpan leadTime)
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

        private IConfigurationManager GetConfigurationManager()
        {
            return _proxyFactory.CreateActorProxy<IConfigurationManager>(new ActorId("configuration"));
        }

        private Modules.ScalingModule GetScalingModule(string name, ScaleManagerConfiguration configuration)
        {
            if (!_modules.TryGetValue(name, out var module))
            {
                _modules.Add(name, _scaleModuleFactory.CreateModule(name, configuration));
            }

            return module;
        }

        private enum ScaleRequestStatus
        {
            InProgress,
            Failing,
            Limited,
            Completed,
        }

        private class ScaleRequest
        {
            private readonly TimeSpan DefaultErrorDelay = TimeSpan.FromSeconds(30);
            private readonly TimeSpan MaxErrorDelay = TimeSpan.FromMinutes(5);

            public int? RequestedThroughput { get; set; }

            public int? FinalThroughput { get; set; }

            public DateTimeOffset? TryAfter { get; set; }

            public TimeSpan? ErrorDelay { get; set; }

            public DateTimeOffset OperationStarted { get; set; }

            public bool IsExecuting => !FinalThroughput.HasValue;

            public ScaleRequestStatus Status
            {
                get
                {
                    if (FinalThroughput.HasValue)
                        return (RequestedThroughput ?? 0) <= FinalThroughput.Value
                            ? ScaleRequestStatus.Completed
                            : ScaleRequestStatus.Limited;
                    else
                        return TryAfter.HasValue ? ScaleRequestStatus.Failing : ScaleRequestStatus.InProgress;
                }
            }

            public ScaleRequest(int? throughput, DateTimeOffset now)
            {
                RequestedThroughput = throughput;
                OperationStarted = now;
            }

            public void RegisterError(DateTimeOffset now)
            {
                if (!ErrorDelay.HasValue)
                {
                    ErrorDelay = DefaultErrorDelay;
                }

                TryAfter = now + (ErrorDelay ?? DefaultErrorDelay);
                ErrorDelay = ErrorDelay.HasValue ? ErrorDelay.Value * 2 : DefaultErrorDelay;
                if (ErrorDelay > MaxErrorDelay)
                    ErrorDelay = MaxErrorDelay;
            }

            public void ResetError()
            {
                ErrorDelay = null;
                TryAfter = null;
            }
        }
    }
}
