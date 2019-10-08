using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Bullfrog.Actors.EventModels;
using Bullfrog.Actors.Interfaces;
using Bullfrog.Actors.Interfaces.Models;
using Bullfrog.Actors.Models;
using Bullfrog.Actors.ResourceScalers;
using Bullfrog.Common;
using Bullfrog.Common.Helpers;
using Bullfrog.DomainEvents;
using Eshopworld.Core;
using Microsoft.ServiceFabric.Actors;
using Microsoft.ServiceFabric.Actors.Client;
using Microsoft.ServiceFabric.Actors.Runtime;

namespace Bullfrog.Actors
{
    [StatePersistence(StatePersistence.Persisted)]
    public class ScaleManager : BullfrogActorBase, IScaleManager, IRemindable
    {
        private const string ReminderName = "wakeupReminder";
        internal static readonly TimeSpan ScanPeriod = TimeSpan.FromMinutes(2);

        private readonly StateItem<List<ManagedScaleEvent>> _events;
        private readonly StateItem<ScaleManagerConfiguration> _configuration;
        private readonly StateItem<Dictionary<Guid, ScaleChangeType>> _reportedEventStates;
        private readonly StateItem<ScalingState> _scaleState;
        private readonly IResourceScalerFactory _scalerFactory;
        private readonly ScaleSetMonitor _scaleSetMonitor;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly IActorProxyFactory _proxyFactory;
        private readonly string _scaleGroupName;
        private readonly string _regionName;
        private readonly Dictionary<string, ResourceScaler> _scalers
            = new Dictionary<string, ResourceScaler>();

        /// <summary>
        /// Initializes a new instance of ScaleManager
        /// </summary>
        /// <param name="actorService">The Microsoft.ServiceFabric.Actors.Runtime.ActorService that will host this actor instance.</param>
        /// <param name="actorId">The Microsoft.ServiceFabric.Actors.ActorId for this actor instance.</param>
        /// <param name="scalerFactory">Scaler factory.</param>
        /// <param name="scaleSetMonitor">Reads performance details of a scale set.</param>
        /// <param name="dateTimeProvider">Gets the current time.</param>
        /// <param name="proxyFactory">Actor proxy factory.</param>
        /// <param name="bigBrother">Performs the logging.</param>
        public ScaleManager(
            ActorService actorService,
            ActorId actorId,
            IResourceScalerFactory scalerFactory,
            ScaleSetMonitor scaleSetMonitor,
            IDateTimeProvider dateTimeProvider,
            IActorProxyFactory proxyFactory,
            IBigBrother bigBrother)
            : base(actorService, actorId, bigBrother)
        {
            _events = new StateItem<List<ManagedScaleEvent>>(StateManager, "scaleEvents");
            _configuration = new StateItem<ScaleManagerConfiguration>(StateManager, "configuration");
            _reportedEventStates = new StateItem<Dictionary<Guid, ScaleChangeType>>(StateManager, "reportedEventStates");
            _scaleState = new StateItem<ScalingState>(StateManager, "scaleState");
            _scalerFactory = scalerFactory;
            _scaleSetMonitor = scaleSetMonitor;
            _dateTimeProvider = dateTimeProvider;
            _proxyFactory = proxyFactory;
            var match = Regex.Match(actorId.ToString(), ":([^/]+)/([^/]+)$");
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

            await _events.TryAdd(new List<ManagedScaleEvent>());
            await FixStateModelSchameChange();
            await _scaleState.TryAdd(new ScalingState());
            await StateManager.TryRemoveStateAsync("scaleOutStarted"); // no longer used

            // The temporary way of dealing with incompatible schame change.
            async Task FixStateModelSchameChange()
            {
                try
                {
                    await _scaleState.TryGet();
                }
                catch (Exception ex)
                {
                    var descriptionEx = new Exception($"Failed to read scale set. Resetting it.", ex);
                    BigBrother.Publish(descriptionEx.ToExceptionEvent());
                    await _scaleState.TryRemove();
                }
            }
        }

        Task IScaleManager.DeleteScaleEvent(Guid id)
        {
            return ((IScaleManager)this).PurgeScaleEvents(new List<Guid> { id });
        }

        async Task IScaleManager.PurgeScaleEvents(List<Guid> events)
        {
            var knownEvents = await _events.Get();
            var toRemove = events.ToHashSet();
            var removed = knownEvents.RemoveAll(x => toRemove.Contains(x.Id));
            if (removed > 0)
                await _events.Set(knownEvents);

            var state = await _scaleState.Get();
            var removedFromState = state.Changes.RemoveAll(x => toRemove.Contains(x.EventId));
            if (removedFromState > 0)
                await _scaleState.Set(state);

            if (removed > 0 || removedFromState > 0)
                await ScheduleStateUpdate();
        }

        async Task<ScaleState> IScaleManager.GetScaleSet()
        {
            var events = await _events.Get();
            var now = _dateTimeProvider.UtcNow;

            var state = await _scaleState.Get();
            if (!state.ScalingOutStartTime.HasValue)
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
                WasScaleUpAt = state.ScalingOutStartTime.Value,
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

            if (configuration.Value.OldEventsAge.HasValue)
            {
                var completedBefore = _dateTimeProvider.UtcNow.Add(-configuration.Value.OldEventsAge.Value);
                events.RemoveAll(e => e.StartScaleDownAt < completedBefore);
            }

            await _events.Set(events);

            await ScheduleStateUpdate();
        }

        async Task IScaleManager.Disable()
        {
            await _events.Set(new List<ManagedScaleEvent>());
            await _configuration.TryRemove();
            await _scaleState.Set(new ScalingState());
            await _reportedEventStates.TryRemove();
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
            try
            {
                await UpdateState();
            }
            catch (Exception ex)
            {
                BigBrother.Publish(ex.ToExceptionEvent());
                await WakeMeAt(_dateTimeProvider.UtcNow.AddMinutes(2));
            }
        }

        private Task ScheduleStateUpdate()
        {
            return WakeMeAt(_dateTimeProvider.UtcNow);
        }

        void AddScaleOutRequests(Dictionary<string, ScaleRequest> scaleRequests, int throughput, DateTimeOffset endsAt,
            string resourceType, IEnumerable<string> names)
            => AddScaleRequest(throughput, scaleRequests, () => new ScaleOutRequest(throughput, endsAt, _dateTimeProvider.UtcNow), resourceType, names);

        void AddScaleInRequests(Dictionary<string, ScaleRequest> scaleRequests, string resourceType, IEnumerable<string> names)
            => AddScaleRequest(null, scaleRequests, () => new ScaleInRequest(_dateTimeProvider.UtcNow), resourceType, names);

        private void AddScaleRequest(int? requestedThroughput, Dictionary<string, ScaleRequest> scaleRequests,
            Func<ScaleRequest> scaleRequestFactory, string resourceType, IEnumerable<string> names)
        {
            bool replacing = false;

            foreach (var name in names)
            {
                if (scaleRequests.TryGetValue(name, out var previous) && previous.IsExecuting)
                    replacing = true;

                scaleRequests[name] = scaleRequestFactory();
            }

            BigBrother.Publish(new ScaleChangeStarted
            {
                ResourceType = resourceType,
                RequestedThroughput = requestedThroughput ?? -1,
                PreviousChangeNotCompleted = replacing,
            });
        }

        private int? CreateScaleRequests(int? requestedThroughput, Dictionary<string, ScaleRequest> scaleRequests,
            List<ManagedScaleEvent> events, IEnumerable<string> resourceNames, TimeSpan leadTime,
            string resourceType, DateTimeOffset now)
        {
            var resources = resourceNames?.ToList();
            if (resources != null && resources.Any())
            {
                var throughput = CalculateTotalThroughput(events, now, leadTime);
                if (throughput != requestedThroughput)
                {
                    // the requested throughput has been changes so scale requests for all resources must be created.
                    if (throughput.HasValue)
                    {
                        var endsAt = FindEndOfActiveEvents(events, now, leadTime);
                        AddScaleOutRequests(scaleRequests, throughput.Value, endsAt.Value, resourceType, resources);
                    }
                    else
                    {
                        AddScaleInRequests(scaleRequests, "scalesets", resources);
                    }
                }

                return throughput;
            }

            return null;
        }

        private async Task UpdateState(CancellationToken cancellationToken = default)
        {
            var configuration = await _configuration.Get(cancellationToken);
            var events = await _events.Get(cancellationToken);
            var state = await _scaleState.Get();
            var now = _dateTimeProvider.UtcNow;

            state.RequestedScaleSetThroughput = CreateScaleRequests(state.RequestedScaleSetThroughput, state.ScaleRequests, events,
                configuration.ScaleSetConfigurations.Select(x => x.Name), configuration.ScaleSetPrescaleLeadTime, "scalesets", now);
            state.RequestedCosmosThroughput = CreateScaleRequests(state.RequestedCosmosThroughput, state.ScaleRequests, events,
                configuration.CosmosConfigurations?.Select(x => x.Name), configuration.CosmosDbPrescaleLeadTime, "cosmosdb", now);

            // Process new or previously created scale change requests
            foreach (var scaleRequestKV in state.ScaleRequests)
            {
                var name = scaleRequestKV.Key;
                var scaleRequest = scaleRequestKV.Value;
                await scaleRequest.Process(_dateTimeProvider.UtcNow, () => GetScaler(name, configuration), BigBrother, name);
            }

            var isScaledOut = state.RequestedScaleSetThroughput.HasValue || state.RequestedCosmosThroughput.HasValue;
            if (isScaledOut)
            {
                if (!state.ScalingOutStartTime.HasValue)
                    state.ScalingOutStartTime = _dateTimeProvider.UtcNow;
            }
            else
            {
                if (state.ScalingOutStartTime.HasValue)
                    state.ScalingOutStartTime = null;
            }

            // Report state of each scaling event. 
            var maxLeadTime = configuration.ScaleSetPrescaleLeadTime < configuration.CosmosDbPrescaleLeadTime
                ? configuration.CosmosDbPrescaleLeadTime
                : configuration.ScaleSetPrescaleLeadTime;
            var finalScale = state.ScaleRequests.Any(o => o.Value.IsExecuting)
                ? null
                : state.ScaleRequests.Min(o => o.Value.CompletedThroughput);
            if (state.Changes == null)
                state.Changes = new List<ScaleEventStateChange>();
            await ReportEventState(events, state.Changes, now, maxLeadTime,
                finalScale,
                state.ScaleRequests.Any(o => o.Value.Status == ScaleRequestStatus.Failing));

            // Find out when to wake up the next time.
            var nextWakeUpTime = state.IsRefreshRequired
                ? _dateTimeProvider.UtcNow.Add(ScanPeriod)
                : FindNextWakeUpTime(
                    events,
                    now,
                    configuration.ScaleSetPrescaleLeadTime,
                    configuration.CosmosDbPrescaleLeadTime);
            await WakeMeAt(nextWakeUpTime);

            if (!isScaledOut && state.ScaleRequests.All(x => !x.Value.IsExecuting))
                state.ScaleRequests.Clear();
            await _scaleState.Set(state);

            var states = state.ScaleRequests.Values.Select(o => o.Status).ToList();
            BigBrother.Publish(new ScaleAgentStatus
            {
                RequestedScaleSetScale = state.RequestedScaleSetThroughput ?? -1,
                RequestedCosmosDbScale = state.RequestedCosmosThroughput ?? -1,
                NextWakeUpTime = nextWakeUpTime,
                ScaleRequests = state.ScaleRequests.Count,
                ScaleCompleted = states.Count(o => o == ScaleRequestStatus.Completed),
                ScaleFailing = states.Count(o => o == ScaleRequestStatus.Failing),
                ScaleInProgress = states.Count(o => o == ScaleRequestStatus.InProgress),
                ScaleLimited = states.Count(o => o == ScaleRequestStatus.Limited),
            });
        }

        private async Task ReportEventState(List<ManagedScaleEvent> events, List<ScaleEventStateChange> changes, DateTimeOffset now,
            TimeSpan leadTime, int? finalScale, bool issuesReported)
        {
            var activeEvents = events.Where(e => e.IsActive(now, leadTime)).ToHashSet();
            var executingEvents = events.Where(e => e.IsActive(now, TimeSpan.Zero)).ToHashSet();
            var reportedEventStates = (await _reportedEventStates.TryGet()).Value ?? new Dictionary<Guid, ScaleChangeType>();

            void ChangeState(Guid id, ScaleChangeType type, bool temporaryIssue = false)
            {
                reportedEventStates[id] = type;
                changes.Add(new ScaleEventStateChange { EventId = id, State = type });
                string details = null;
                if (type == ScaleChangeType.ScaleIssue)
                    details = temporaryIssue ? "temporary" : "permanent";
                BigBrother.Publish(new EventRegionScaleChange
                {
                    Id = id,
                    RegionName = _regionName,
                    ScaleGroup = _scaleGroupName,
                    Type = type,
                    Details = details,
                });
            }

            void HandleScaleOutCompleted(IEnumerable<ManagedScaleEvent> eventsGroup)
            {
                var scaleCompletedState = eventsGroup.Sum(e => e.Scale) <= finalScale
                    ? ScaleChangeType.ScaleOutComplete
                    : ScaleChangeType.ScaleIssue;

                var startingEvents = from e in eventsGroup
                                     let state = reportedEventStates[e.Id]
                                     where state == ScaleChangeType.ScaleOutStarted || state == ScaleChangeType.ScaleIssue
                                     select e.Id;

                foreach (var evId in startingEvents)
                {
                    ChangeState(evId, scaleCompletedState, temporaryIssue: false);
                }
            }

            foreach (var ev in activeEvents.Where(e => !reportedEventStates.ContainsKey(e.Id)))
            {
                ChangeState(ev.Id, ScaleChangeType.ScaleOutStarted);
            }

            if (issuesReported)
            {
                foreach (var ev in reportedEventStates.Where(e => e.Value == ScaleChangeType.ScaleOutStarted).ToList())
                    ChangeState(ev.Key, ScaleChangeType.ScaleIssue, temporaryIssue: true);
            }
            else if (finalScale.HasValue)
            {
                HandleScaleOutCompleted(executingEvents);
                HandleScaleOutCompleted(activeEvents);
            }
            else
            {
                foreach (var ev in reportedEventStates.Where(e => e.Value == ScaleChangeType.ScaleIssue).ToList())
                    ChangeState(ev.Key, ScaleChangeType.ScaleOutStarted);
            }

            foreach (var key in reportedEventStates.Keys.Except(activeEvents.Select(e => e.Id)).ToList())
            {
                if (reportedEventStates[key] != ScaleChangeType.ScaleInStarted)
                    ChangeState(key, ScaleChangeType.ScaleInStarted);
            }

            if (finalScale.HasValue)
            {
                foreach (var ev in reportedEventStates.Where(e => e.Value == ScaleChangeType.ScaleInStarted).ToList())
                {
                    ChangeState(ev.Key, ScaleChangeType.ScaleInComplete);
                    reportedEventStates.Remove(ev.Key);
                }
            }

            if (changes.Any())
            {
                try
                {
                    await _proxyFactory.GetScaleEventStateReporter(_scaleGroupName).ReportScaleEventState(_regionName, changes);
                    changes.Clear();
                }
                catch (Exception ex)
                {
                    BigBrother.Publish(ex.ToExceptionEvent());
                }

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

        private static DateTimeOffset? FindEndOfActiveEvents(IEnumerable<ManagedScaleEvent> events, DateTimeOffset now, TimeSpan leadTime)
        {
            return events
                .Where(e => e.IsActive(now, leadTime))
                .Max(x => (DateTimeOffset?)x.StartScaleDownAt);
        }

        private static DateTimeOffset? FindNextWakeUpTime(IEnumerable<ManagedScaleEvent> events, DateTimeOffset now, TimeSpan scaleSetLeadTime, TimeSpan cosmosDbLeadTime)
        {
            var nextTime = events.SelectMany(ev => new[] { ev.StartScaleDownAt, ev.RequiredScaleAt, ev.RequiredScaleAt - cosmosDbLeadTime, ev.RequiredScaleAt - scaleSetLeadTime })
                .Where(t => t > now)
                .Cast<DateTimeOffset?>()
                .Min();

            return nextTime;
        }

        private ResourceScaler GetScaler(string name, ScaleManagerConfiguration configuration)
        {
            if (!_scalers.TryGetValue(name, out var scaler))
            {
                scaler = _scalerFactory.CreateScaler(name, configuration);
                System.Diagnostics.Debug.Assert(scaler != null);
                _scalers.Add(name, scaler);
            }

            return scaler;
        }

        [DataContract]
        private class ScalingState
        {
            [DataMember]
            public DateTimeOffset? ScalingOutStartTime { get; set; }

            [DataMember]
            public Dictionary<string, ScaleRequest> ScaleRequests { get; set; }
                = new Dictionary<string, ScaleRequest>();

            [DataMember]
            public List<ScaleEventStateChange> Changes { get; set; }
                = new List<ScaleEventStateChange>();

            [DataMember]
            public int? RequestedScaleSetThroughput { get; set; }

            [DataMember]
            public int? RequestedCosmosThroughput { get; set; }

            /// <summary>
            /// Informs whether the state should be refreshed shortly.
            /// </summary>
            public bool IsRefreshRequired => ScaleRequests.Any(o => o.Value.IsExecuting) || Changes.Any();
        }

        private enum ScaleRequestStatus
        {
            InProgress,
            Failing,
            Limited,
            Completed,
        }

        [DataContract]
        [KnownType(typeof(ScaleOutRequest))]
        [KnownType(typeof(ScaleInRequest))]
        private abstract class ScaleRequest
        {
            private static readonly TimeSpan DefaultErrorDelay = TimeSpan.FromMinutes(1);
            private static readonly TimeSpan MaxErrorDelay = TimeSpan.FromMinutes(5);

            [DataMember]
            public DateTimeOffset? TryAfter { get; set; }

            [DataMember]
            public TimeSpan? ErrorDelay { get; set; }

            [DataMember]
            public DateTimeOffset OperationStarted { get; set; }

            public abstract bool IsExecuting { get; }

            public abstract int? CompletedThroughput { get; }

            protected abstract ScaleRequestStatus CompletionStatus { get; }

            public ScaleRequestStatus Status
            {
                get
                {
                    if (IsExecuting)
                        return TryAfter.HasValue ? ScaleRequestStatus.Failing : ScaleRequestStatus.InProgress;
                    else
                        return CompletionStatus;
                }
            }

            protected ScaleRequest(DateTimeOffset now)
            {
                OperationStarted = now;
            }

            public async Task Process(DateTimeOffset now, Func<ResourceScaler> getScaler, IBigBrother bigBrother, string resourceName)
            {
                if (!IsExecuting)
                    return;

                if (now < TryAfter)
                    return;

                try
                {
                    await ProcessRequest(getScaler());
                    ResetError();

                    if (!IsExecuting)
                    {
                        var ev = new ResourceScalingCompleted
                        {
                            Duration = now - OperationStarted,
                            ResourceName = resourceName,
                        };
                        UpdateCompletionEvent(ev);
                        bigBrother.Publish(ev);
                    }
                }
                catch (Exception ex)
                {
                    RegisterError(now);
                    var contextEx = new BullfrogException($"Failed to {OperationDescription(resourceName)}", ex);
                    bigBrother.Publish(contextEx.ToExceptionEvent());
                }
            }

            protected abstract void UpdateCompletionEvent(ResourceScalingCompleted ev);

            protected abstract string OperationDescription(string resourceName);

            protected abstract Task ProcessRequest(ResourceScaler scaler);

            private void RegisterError(DateTimeOffset now)
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

            private void ResetError()
            {
                ErrorDelay = null;
                TryAfter = null;
            }
        }

        [DataContract]
        private class ScaleOutRequest : ScaleRequest
        {
            private static readonly TimeSpan EndsAtHintDelay = TimeSpan.FromMinutes(3);

            [DataMember]
            public int RequestedThroughput { get; set; }

            [DataMember]
            public DateTimeOffset EndsAt { get; set; }

            [DataMember]
            public int? FinalThroughput { get; set; }

            public override int? CompletedThroughput => FinalThroughput;

            public override bool IsExecuting => !FinalThroughput.HasValue;

            protected override ScaleRequestStatus CompletionStatus => RequestedThroughput <= FinalThroughput.Value
                       ? ScaleRequestStatus.Completed
                       : ScaleRequestStatus.Limited;

            public ScaleOutRequest(int throughput, DateTimeOffset endsAt, DateTimeOffset now)
                : base(now)
            {
                RequestedThroughput = throughput;
                EndsAt = endsAt;
            }

            protected override void UpdateCompletionEvent(ResourceScalingCompleted ev)
            {
                ev.FinalThroughput = FinalThroughput.Value;
                ev.RequiredThroughput = RequestedThroughput;
            }

            protected override async Task ProcessRequest(ResourceScaler scaler)
            {
                FinalThroughput = await scaler.ScaleOut(RequestedThroughput, EndsAt + EndsAtHintDelay);
            }

            protected override string OperationDescription(string resourceName)
                => $"scale out {resourceName} to {RequestedThroughput}";
        }

        [DataContract]
        private class ScaleInRequest : ScaleRequest
        {
            [DataMember]
            public bool Completed { get; set; }

            public override bool IsExecuting => !Completed;

            public override int? CompletedThroughput => Completed ? 0 : (int?)null;

            protected override ScaleRequestStatus CompletionStatus => ScaleRequestStatus.Completed;

            public ScaleInRequest(DateTimeOffset now)
                : base(now)
            {
            }

            protected override async Task ProcessRequest(ResourceScaler scaler)
            {
                Completed = await scaler.ScaleIn();
            }

            protected override string OperationDescription(string resourceName)
                => $"scale in {resourceName}";

            protected override void UpdateCompletionEvent(ResourceScalingCompleted ev)
            {
            }
        }
    }
}
