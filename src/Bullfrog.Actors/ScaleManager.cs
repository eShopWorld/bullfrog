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
using Bullfrog.Common.Models;
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
        private readonly StateItem<ScaleManagerFeatureFlags> _featureFlags;
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
            _featureFlags = new StateItem<ScaleManagerFeatureFlags>(StateManager, "featureFlags");
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
            await _scaleState.TryAdd(new ScalingState());
            await StateManager.TryRemoveStateAsync("scaleOutStarted"); // no longer used
        }

        Task IScaleManager.DeleteScaleEvent(Guid id)
        {
            try
            {
                return ((IScaleManager)this).PurgeScaleEvents(new List<Guid> { id });
            }
            catch (Exception ex)
            {
                BigBrother.Publish(ex.ToExceptionEvent());
                throw;
            }
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
            try
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
            catch (Exception ex)
            {
                BigBrother.Publish(ex.ToExceptionEvent());
                throw;
            }
        }

        async Task<List<RegionScaleEvent>> IScaleManager.ListEvents()
        {
            var events = await _events.Get();
            return events
                .Select(ev => new RegionScaleEvent
                {
                    Id = ev.Id,
                    Name = ev.Name,
                    RequiredScaleAt = ev.RequiredScaleAt,
                    Scale = ev.Scale,
                    StartScaleDownAt = ev.StartScaleDownAt,
                })
                .ToList();
        }

        async Task IScaleManager.Disable()
        {
            try
            {
                var configuration = await _configuration.TryGet();
                if (!configuration.HasValue)
                    return;

                await ConfigureResourceScalingActors(configuration.Value, null);

                await _events.Set(new List<ManagedScaleEvent>());
                await _configuration.TryRemove();
                await _scaleState.Set(new ScalingState());
                await _reportedEventStates.TryRemove();
                await WakeMeAt(null);

            }
            catch (Exception ex)
            {
                BigBrother.Publish(ex.ToExceptionEvent());
                throw;
            }
        }

        async Task IScaleManager.Configure(ScaleManagerConfiguration configuration, FeatureFlagsConfiguration featureFlags)
        {
            try
            {
                var oldConfiguration = await _configuration.TryGet();
                var scaleManagerFeatureFlags = new ScaleManagerFeatureFlags
                {
                    UseScalingActors = featureFlags.ResourceScallersEnabled ?? false,
                };

                await ConfigureResourceScalingActors(oldConfiguration.Value, configuration);

                configuration.ScaleGroup = _scaleGroupName;
                configuration.Region = _regionName;
                configuration.UseScalingActors = featureFlags.ResourceScallersEnabled ?? false;
                await _configuration.Set(configuration);

                var estimatedScaleTime = TimeSpan.FromTicks(Math.Max(configuration.CosmosDbPrescaleLeadTime.Ticks,
                    configuration.ScaleSetPrescaleLeadTime.Ticks));
                var events = await _events.Get();
                foreach (var ev in events)
                {
                    ev.EstimatedScaleUpAt = ev.RequiredScaleAt - estimatedScaleTime;
                }
                await _events.Set(events);
                await _featureFlags.Set(scaleManagerFeatureFlags);
                await ScheduleStateUpdate();
            }
            catch (Exception ex)
            {
                BigBrother.Publish(ex.ToExceptionEvent());
                throw;
            }
        }

        async Task IScaleManager.SetFeatureFlags(FeatureFlagsConfiguration featureFlags)
        {
            await _featureFlags.Set(new ScaleManagerFeatureFlags
            {
                UseScalingActors = featureFlags.ResourceScallersEnabled ?? false,
            });
        }

        private async Task ConfigureResourceScalingActors(ScaleManagerConfiguration oldConfiguration, ScaleManagerConfiguration newConfiguration)
        {
            var newResources = GetResources(newConfiguration);
            var oldResources = GetResources(oldConfiguration);

            // Update configuration of existing actors or configure new ones.
            foreach (var kv in newResources)
            {
                oldResources.Remove(kv.Key);
                var resourceScalingActor = _proxyFactory.GetResourceScalingActor(_scaleGroupName, _regionName, kv.Key);
                await resourceScalingActor.Configure(kv.Value);
            }

            // Disable previously used actors not longer configured.
            foreach (var kv in oldResources)
            {
                var resourceScalingActor = _proxyFactory.GetResourceScalingActor(_scaleGroupName, _regionName, kv.Key);
                await resourceScalingActor.Configure(null);
            }

            // A helper method which lists all configured resources (scale sets and Cosmos dbs)
            Dictionary<string, ResourceScalingActorConfiguration> GetResources(ScaleManagerConfiguration configuration)
            {
                var resources = new Dictionary<string, ResourceScalingActorConfiguration>();
                if (configuration != null)
                {
                    foreach (var vmssConfiguration in configuration.ScaleSetConfigurations)
                    {
                        resources.Add(vmssConfiguration.Name, new ResourceScalingActorConfiguration
                        {
                            AutomationAccounts = configuration.AutomationAccounts,
                            ScaleSetConfiguration = vmssConfiguration,
                        });
                    }

                    if (configuration.CosmosConfigurations != null)
                        foreach (var cosmosConfiguration in configuration.CosmosConfigurations)
                        {
                            resources.Add(cosmosConfiguration.Name, new ResourceScalingActorConfiguration
                            {
                                AutomationAccounts = configuration.AutomationAccounts,
                                CosmosConfiguration = cosmosConfiguration,
                            });
                        }
                }

                return resources;
            }
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

        /// <summary>
        /// Adds a scale operation to perfom on a resource.
        /// </summary>
        /// <param name="requestedThroughput">The requested throughput or null for scale in</param>
        /// <param name="scaleRequests">All current scale operations for each resource.</param>
        /// <param name="scaleRequestFactory">The factory of the scale operation class instances.</param>
        /// <param name="resourceType">The type of the resource (a scale set or cosmos db) for telemetry</param>
        /// <param name="names">The names of all resources that should be scaled.</param>
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
                        AddScaleInRequests(scaleRequests, resourceType, resources);
                    }
                }

                return throughput;
            }

            return null;
        }

        /// <summary>
        /// The main periodically executed method which checkes the current state of actor, its list of events and
        /// performes required operations.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns>Task</returns>
        private async Task UpdateState(CancellationToken cancellationToken = default)
        {
            var configuration = await _configuration.Get(cancellationToken);
            var events = await _events.Get(cancellationToken);
            var state = await _scaleState.Get();
            var now = _dateTimeProvider.UtcNow;

            // Calculate the current requested throughput updating the state of scale requests for each resource at the same time.
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

            // If the scaling operation has just started update state variables used for reporting.
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

            // Find all scale events which execution status has just changed and details about them to ScaleEventStateReporter actor.
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
                ? _dateTimeProvider.UtcNow.Add(ScanPeriod) // some operations are in progress. Wake up to monitor and report their state.
                : FindNextWakeUpTime(
                    events,
                    now,
                    configuration.ScaleSetPrescaleLeadTime,
                    configuration.CosmosDbPrescaleLeadTime); // no operations are in progress, so look for next time something must happen.
            await WakeMeAt(nextWakeUpTime);

            // Clear and save state.
            if (!isScaledOut && state.ScaleRequests.All(x => !x.Value.IsExecuting))
                state.ScaleRequests.Clear();
            await _scaleState.Set(state);

            // Publish the current state of the actor.
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

        /// <summary>
        /// Compares the previous and current state of events to look for and report their changes.
        /// </summary>
        /// <param name="events">The list of events.</param>
        /// <param name="changes">The list of changes which should be send to <see cref="ScaleEventStateReporter"/> actor.</param>
        /// <param name="now">The current time.</param>
        /// <param name="leadTime">The scaling lead time.</param>
        /// <param name="finalScale">The throughput available afeter completion of scaling or null if scaling is still in progress.</param>
        /// <param name="issuesReported">Specifies whether any scaler reported issues recently.</param>
        /// <returns>Task</returns>
        private async Task ReportEventState(List<ManagedScaleEvent> events, List<ScaleEventStateChange> changes, DateTimeOffset now,
            TimeSpan leadTime, int? finalScale, bool issuesReported)
        {
            // The scale events which starts soon or are already executing.
            var activeEvents = events.Where(e => e.IsActive(now, leadTime)).ToHashSet();

            // The scale events which are currently executing.
            var executingEvents = events.Where(e => e.IsActive(now, TimeSpan.Zero)).ToHashSet();

            // a dictionary with reported state for each scale event.
            var reportedEventStates = (await _reportedEventStates.TryGet()).Value ?? new Dictionary<Guid, ScaleChangeType>();

            // The helper method to update details about status of each scale event
            void ChangeState(Guid id, ScaleChangeType type, bool isIssueTemporary = false)
            {
                reportedEventStates[id] = type;
                changes.Add(new ScaleEventStateChange { EventId = id, State = type });
                string details = null;
                if (type == ScaleChangeType.ScaleIssue)
                    details = isIssueTemporary ? "temporary" : "permanent";
                BigBrother.Publish(new EventRegionScaleChange
                {
                    Id = id,
                    RegionName = _regionName,
                    ScaleGroup = _scaleGroupName,
                    Type = type,
                    Details = details,
                });
            }

            // The helper method to handle all scale events which completed scaling out.
            void HandleScaleOutCompleted(IEnumerable<ManagedScaleEvent> eventsGroup)
            {
                // Calculate how big total throughput is requested by scale events from the events group
                var scaleCompletedState = eventsGroup.Sum(e => e.Scale) <= finalScale
                    ? ScaleChangeType.ScaleOutComplete
                    : ScaleChangeType.ScaleIssue;

                // Only update status of events which are in the middle of scalling out (possibly with issues)
                var startingEvents = from e in eventsGroup
                                     let state = reportedEventStates[e.Id]
                                     where state == ScaleChangeType.ScaleOutStarted || state == ScaleChangeType.ScaleIssue
                                     select e.Id;

                foreach (var evId in startingEvents)
                {
                    ChangeState(evId, scaleCompletedState, isIssueTemporary: false);
                }
            }

            // Look for new events which have just started being processed
            foreach (var ev in activeEvents.Where(e => !reportedEventStates.ContainsKey(e.Id)))
            {
                ChangeState(ev.Id, ScaleChangeType.ScaleOutStarted);
            }

            if (issuesReported)
            {
                // In case of errors mark all events which currently scaling out as problematic.
                foreach (var ev in reportedEventStates.Where(e => e.Value == ScaleChangeType.ScaleOutStarted).ToList())
                    ChangeState(ev.Key, ScaleChangeType.ScaleIssue, isIssueTemporary: true);
            }
            else if (finalScale.HasValue)
            {
                // The scaling out completed. Depending on the available throughput mark all or only some events as completed
                // (potentially with issues)
                HandleScaleOutCompleted(executingEvents);
                HandleScaleOutCompleted(activeEvents);
            }
            else
            {
                // The scaling out is still in progress and no errors have been reported so previous warnings can be removed.
                foreach (var ev in reportedEventStates.Where(e => e.Value == ScaleChangeType.ScaleIssue).ToList())
                    ChangeState(ev.Key, ScaleChangeType.ScaleOutStarted);
            }

            // Report start of scale in for each no longer active event.
            foreach (var key in reportedEventStates.Keys.Except(activeEvents.Select(e => e.Id)).ToList())
            {
                if (reportedEventStates[key] != ScaleChangeType.ScaleInStarted)
                    ChangeState(key, ScaleChangeType.ScaleInStarted);
            }

            if (finalScale.HasValue)
            {
                // Scaling operation has completed so not active events can be moved to the ScaleInComplete phase and be forgotten
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
                    // send all changes of scale event statuses (if any) and clear the list if the send is successful.
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

        /// <summary>
        /// Sets up a reminder to wake up the actor at the specified time or removes a wake up reminder.
        /// </summary>
        /// <param name="time">The time when the actor should be woken up or null if there's no reason to wake up the actor.</param>
        /// <returns></returns>
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

        /// <summary>
        /// Calculates the total thoughput required to handle all active scale events 
        /// </summary>
        /// <param name="events">The list of events.</param>
        /// <param name="now">The currrent time.</param>
        /// <param name="leadTime">The scaling lead time.</param>
        /// <returns>The total throughput requested or null if there's no active events.</returns>
        private static int? CalculateTotalThroughput(IEnumerable<ManagedScaleEvent> events, DateTimeOffset now, TimeSpan leadTime)
        {
            int? total = null;
            foreach (var ev in events.Where(e => e.IsActive(now, leadTime)))
            {
                total = checked(ev.Scale + total.GetValueOrDefault());
            }

            return total;
        }

        /// <summary>
        /// Calculates the date when the last currently active event ends.
        /// </summary>
        /// <param name="events">The list of events.</param>
        /// <param name="now">The currrent time.</param>
        /// <param name="leadTime">The scaling lead time.</param>
        /// <returns>The end of the last active event or null if there's no active events.</returns>
        private static DateTimeOffset? FindEndOfActiveEvents(IEnumerable<ManagedScaleEvent> events, DateTimeOffset now, TimeSpan leadTime)
        {
            return events
                .Where(e => e.IsActive(now, leadTime))
                .Max(x => (DateTimeOffset?)x.StartScaleDownAt);
        }

        /// <summary>
        /// Calculates the time when the actor should wake up to handle some kind of state change.
        /// </summary>
        /// <param name="events">The list of events.</param>
        /// <param name="now">The currrent time.</param>
        /// <param name="scaleSetLeadTime">The scale set scaling lead time.</param>
        /// <param name="cosmosDbLeadTime">The Cosmos scaling lead time.</param>
        /// <returns>The time of the nearest state change or null if there's no mo events to handle.</returns>
        private static DateTimeOffset? FindNextWakeUpTime(IEnumerable<ManagedScaleEvent> events, DateTimeOffset now, TimeSpan scaleSetLeadTime, TimeSpan cosmosDbLeadTime)
        {
            var nextTime = events.SelectMany(ev => new[] { ev.StartScaleDownAt, ev.RequiredScaleAt, ev.RequiredScaleAt - cosmosDbLeadTime, ev.RequiredScaleAt - scaleSetLeadTime })
                .Where(t => t > now)
                .Cast<DateTimeOffset?>()
                .Min();

            return nextTime;
        }

        /// <summary>
        /// Finds a scaler resposible for handling scaling of the named resource.
        /// </summary>
        /// <param name="name">The name of the resource (scale set or cosmos)</param>
        /// <param name="configuration">The scale manager configuration.</param>
        /// <returns></returns>
        private ResourceScaler GetScaler(string name, ScaleManagerConfiguration configuration)
        {
            if (!_scalers.TryGetValue(name, out var scaler))
            {
                if (configuration.UseScalingActors)
                {
                    var resourceScalingActor = _proxyFactory.GetResourceScalingActor(_scaleGroupName, _regionName, name);
                    scaler = new ActorProxyResourceScaler(resourceScalingActor);
                }
                else
                {
                    scaler = _scalerFactory.CreateScaler(name, configuration);
                }

                System.Diagnostics.Debug.Assert(scaler != null);
                _scalers.Add(name, scaler);
            }

            return scaler;
        }


        /// <summary>
        /// The state of the scaling actor.
        /// </summary>
        [DataContract]
        private class ScalingState
        {
            /// <summary>
            /// When the latest scaling out operation started (or null if there's no active events currently).
            /// </summary>
            [DataMember]
            public DateTimeOffset? ScalingOutStartTime { get; set; }

            /// <summary>
            /// The scaling state of each resource.
            /// </summary>
            [DataMember]
            public Dictionary<string, ScaleRequest> ScaleRequests { get; set; }
                = new Dictionary<string, ScaleRequest>();

            /// <summary>
            /// The recent state transitions of scale events.
            /// </summary>
            /// <remarks>
            /// This list is cleared after passign this data to <see cref="ScaleEventStateReporter"/> actor.
            /// </remarks>
            [DataMember]
            public List<ScaleEventStateChange> Changes { get; set; }
                = new List<ScaleEventStateChange>();

            /// <summary>
            /// The throughput requested currently from scale sets (null if none).
            /// </summary>
            [DataMember]
            public int? RequestedScaleSetThroughput { get; set; }

            /// <summary>
            /// The throughput requested currently from Cosmos (null if none).
            /// </summary>
            [DataMember]
            public int? RequestedCosmosThroughput { get; set; }

            /// <summary>
            /// Informs whether the state should be refreshed shortly.
            /// </summary>
            public bool IsRefreshRequired => ScaleRequests.Any(o => o.Value.IsExecuting) || Changes.Any();
        }

        /// <summary>
        /// The status of a scaled resource.
        /// </summary>
        private enum ScaleRequestStatus
        {
            /// <summary>
            /// The scaling has been started and has not yet completed.
            /// </summary>
            InProgress,

            /// <summary>
            /// Some issues has been reported but the scaling is still in progress.
            /// </summary>
            Failing,

            /// <summary>
            /// The scaling has completed but it hasn't reached the requested throughput
            /// </summary>
            Limited,

            /// <summary>
            /// The scaling has completed reaching the requested throughput.
            /// </summary>
            Completed,
        }

        /// <summary>
        /// Represents the resources scaling operations.
        /// </summary>
        /// <remarks>
        /// It's a state machine which keeps track and manages scale in and scale out operations.
        /// </remarks>
        [DataContract]
        [KnownType(typeof(ScaleOutRequest))]
        [KnownType(typeof(ScaleInRequest))]
        private abstract class ScaleRequest
        {
            /// <summary>
            /// The default delay after an error is reported for the first time. 
            /// </summary>
            private static readonly TimeSpan DefaultErrorDelay = TimeSpan.FromMinutes(1);

            /// <summary>
            /// The longest interval between checks when errors are reported.
            /// </summary>
            private static readonly TimeSpan MaxErrorDelay = TimeSpan.FromMinutes(5);

            /// <summary>
            /// The next time the resource state should be checked.
            /// </summary>
            [DataMember]
            public DateTimeOffset? TryAfter { get; set; }

            /// <summary>
            /// The current delay after the previous error report (or null if no error has been recently reported)
            /// </summary>
            [DataMember]
            public TimeSpan? ErrorDelay { get; set; }

            /// <summary>
            /// The time when the scaling operation has started.
            /// </summary>
            [DataMember]
            public DateTimeOffset OperationStarted { get; set; }

            /// <summary>
            /// The optional serialized state of the resource scaler that should be preserved between executions.
            /// </summary>
            [DataMember]
            public string SerializedResourceScalerState { get; set; }

            /// <summary>
            /// Informs whether scaling operation is still in progress.
            /// </summary>
            public abstract bool IsExecuting { get; }

            /// <summary>
            /// The throughput reached after the operation completed.
            /// </summary>
            public abstract int? CompletedThroughput { get; }

            /// <summary>
            /// Returns the status of the completed operation.
            /// </summary>
            protected abstract ScaleRequestStatus CompletionStatus { get; }

            /// <summary>
            /// The current status of the resource scaler.
            /// </summary>
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

            /// <summary>
            /// Creates an instance of the scale request.
            /// </summary>
            /// <param name="now">The current time.</param>
            protected ScaleRequest(DateTimeOffset now)
            {
                OperationStarted = now;
            }

            /// <summary>
            /// Performs a single step in processing scaling operation.
            /// </summary>
            /// <param name="now">The current time.</param>
            /// <param name="getScaler">The scaler factory.</param>
            /// <param name="bigBrother">The telemetry.</param>
            /// <param name="resourceName">The scaled resource name</param>
            /// <returns></returns>
            public async Task Process(DateTimeOffset now, Func<ResourceScaler> getScaler, IBigBrother bigBrother, string resourceName)
            {
                if (!IsExecuting)
                    return;

                if (now < TryAfter)
                    return;

                try
                {
                    await PerformScaling(getScaler());
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

            /// <summary>
            /// Updates the telemetry event if necessary.
            /// </summary>
            /// <param name="ev"></param>
            protected abstract void UpdateCompletionEvent(ResourceScalingCompleted ev);

            /// <summary>
            /// Returns the scaling operation description used in telemetry.
            /// </summary>
            /// <param name="resourceName">The resource name</param>
            /// <returns>The description of the scaling operation.</returns>
            protected abstract string OperationDescription(string resourceName);

            /// <summary>
            /// Executes operation type specific action (e.g. calls Azure Managment API).
            /// </summary>
            /// <param name="scaler">The scaler.</param>
            /// <returns>Task.</returns>
            protected abstract Task ProcessRequest(ResourceScaler scaler);

            /// <summary>
            /// Executes scaling operation.
            /// </summary>
            /// <param name="scaler">The scaler</param>
            private async Task PerformScaling(ResourceScaler scaler)
            {
                scaler.SerializedState = SerializedResourceScalerState;
                await ProcessRequest(scaler);
                SerializedResourceScalerState = scaler.SerializedState;
            }

            /// <summary>
            /// Sets the time of the next attempt to scale a resource after receiving an error.
            /// </summary>
            /// <param name="now">The current time.</param>
            private void RegisterError(DateTimeOffset now)
            {
                if (!ErrorDelay.HasValue)
                {
                    ErrorDelay = DefaultErrorDelay;
                }
                else
                {
                    ErrorDelay = ErrorDelay.Value * 2;
                    if (ErrorDelay > MaxErrorDelay)
                        ErrorDelay = MaxErrorDelay;
                }

                TryAfter = now + ErrorDelay;
            }

            /// <summary>
            /// Returns to normal processing of the scaling operation after previously reported error has been fixed.
            /// </summary>
            private void ResetError()
            {
                ErrorDelay = null;
                TryAfter = null;
            }
        }

        /// <summary>
        /// Handles the scaling out of a resource.
        /// </summary>
        [DataContract]
        private sealed class ScaleOutRequest : ScaleRequest
        {
            /// <summary>
            /// Defines how much longer than necessary the resource should be scaled out.
            /// </summary>
            /// <remarks>
            /// This is used only by fixed time profiles of autoscaling settings. The scale in still can happen
            /// ealrier and delete/update this profile.
            /// </remarks>
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

        /// <summary>
        /// Handles the scaling in of a resource.
        /// </summary>
        [DataContract]
        private sealed class ScaleInRequest : ScaleRequest
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
