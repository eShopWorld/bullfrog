using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using Bullfrog.Actors.Interfaces;
using Bullfrog.Actors.Interfaces.Models;
using Bullfrog.Actors.Models;
using Bullfrog.Common;
using Bullfrog.DomainEvents;
using Eshopworld.Core;
using Microsoft.ServiceFabric.Actors;
using Microsoft.ServiceFabric.Actors.Client;
using Microsoft.ServiceFabric.Actors.Runtime;

namespace Bullfrog.Actors
{
    [StatePersistence(StatePersistence.Persisted)]
    public class ConfigurationManager : BullfrogActorBase, IConfigurationManager
    {
        private const string ScaleGroupKeyPrefix = "scaleGroup:";
        private const string EventsListKeyPrefix = "events:";
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly IActorProxyFactory _proxyFactory;

        /// <summary>
        /// Initializes a new instance of ScaleManager
        /// </summary>
        /// <param name="actorService">The Microsoft.ServiceFabric.Actors.Runtime.ActorService that will host this actor instance.</param>
        /// <param name="dateTimeProvider">The provider of the current time.</param>
        /// <param name="actorId">The Microsoft.ServiceFabric.Actors.ActorId for this actor instance.</param>
        /// <param name="proxyFactory">The factory of actor client proxies.</param>
        /// <param name="bigBrother">The Big Brother client.</param>
        public ConfigurationManager(ActorService actorService,
            ActorId actorId,
            IDateTimeProvider dateTimeProvider,
            IActorProxyFactory proxyFactory,
            IBigBrother bigBrother)
            : base(actorService, actorId, bigBrother)
        {
            _dateTimeProvider = dateTimeProvider;
            _proxyFactory = proxyFactory;
        }

        /// <summary>
        /// This method is called whenever an actor is activated.
        /// An actor is activated the first time any of its methods are invoked.
        /// </summary>
        protected override Task OnActivateAsync()
        {
            ActorEventSource.Current.ActorMessage(this, "ConfigurationManager actor activated.");
            return Task.CompletedTask;
        }

        [SuppressMessage("Major Code Smell", "S4457:Parameter validation in \"async\"/\"await\" methods should be wrapped", Justification = "Not necessary")]
        async Task IConfigurationManager.ConfigureScaleGroup(string name, ScaleGroupDefinition definition)
        {
            if (string.IsNullOrWhiteSpace(name) || name.Trim() != name)
            {
                throw new ArgumentException("The name parameter is invalid", nameof(name));
            }

            var state = GetScaleGroupState(name);

            var existingGroups = await state.TryGet();

            if (definition != null)
            {
                // Handle creation or updating of a scale group.
                if (existingGroups.HasValue)
                {
                    // Make sure no existing region used by a scale event is removed.
                    var removedRegions = existingGroups.Value.Regions.Select(r => r.RegionName)
                        .Except(definition.Regions.Select(r => r.RegionName))
                        .ToHashSet();

                    if (removedRegions.Any())
                    {
                        var events = (await GetScaleEventsStateItem(name).Get());
                        if (events.Any(e => e.Value.Regions.Keys.Intersect(removedRegions).Any()))
                        {
                            throw new InvalidRequestException("The region used by scale events cannot be removed.");
                        }
                    }
                }

                await UpdateScaleManagers(name, definition, existingGroups);
                await state.Set(definition, default);
            }
            else if (existingGroups.HasValue)
            {
                // Delete the scale group if it has been registered.
                await DisableRegions(name, existingGroups.Value.Regions.Select(r => r.RegionName), existingGroups.Value.HasSharedCosmosDb);
                await state.Remove(default);
            }

            var eventsList = GetScaleEventsStateItem(name);
            await eventsList.TryAdd(new Dictionary<Guid, RegisteredScaleEvent>());
        }

        async Task<List<string>> IConfigurationManager.ListConfiguredScaleGroup()
        {
            var names = await StateManager.GetStateNamesAsync();
            return names
                .Where(n => n.StartsWith(ScaleGroupKeyPrefix))
                .Select(n => n.Substring(ScaleGroupKeyPrefix.Length))
                .ToList();
        }

        [SuppressMessage("Major Code Smell", "S4457:Parameter validation in \"async\"/\"await\" methods should be wrapped", Justification = "Not necessary")]
        async Task<ScaleGroupDefinition> IConfigurationManager.GetScaleGroupConfiguration(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || name.Trim() != name)
            {
                throw new ArgumentException("The name parameter is invalid", nameof(name));
            }
            var state = GetScaleGroupState(name);
            var configuration = await state.TryGet();
            return configuration.HasValue ? configuration.Value : null;
        }

        async Task<ScheduledScaleEvent> IConfigurationManager.GetScaleEvent(string scaleGroup, Guid eventId)
        {
            var scaleGroupDefinition = await GetScaleGroupDefinition(scaleGroup);
            var scaleEventsState = GetScaleEventsStateItem(scaleGroup);
            var list = await scaleEventsState.Get();
            if (list.TryGetValue(eventId, out var scaleEvent))
            {
                var leadTime = scaleEvent.Regions
                 .Select(r => scaleGroupDefinition[r.Key].ScaleSetPrescaleLeadTime)
                 .Max();
                return scaleEvent.ToScheduledScaleEvent(eventId, leadTime);
            }
            else
            {
                throw new ScaleEventNotFoundException();
            }
        }

        async Task<List<ScheduledScaleEvent>> IConfigurationManager.ListScaleEvents(string scaleGroup)
        {
            var scaleGroupDefinition = await GetScaleGroupDefinition(scaleGroup);
            var scaleEventsState = GetScaleEventsStateItem(scaleGroup);
            var list = await scaleEventsState.Get();
            return list.Select(r => r.Value.ToScheduledScaleEvent(r.Key, LeadTime(r.Value))).ToList();

            TimeSpan LeadTime(RegisteredScaleEvent scaleEvent) => scaleEvent.Regions
                .Select(r => scaleGroupDefinition[r.Key].ScaleSetPrescaleLeadTime)
                .Max();
        }

        async Task<SaveScaleEventReturnValue> IConfigurationManager.SaveScaleEvent(string scaleGroup, Guid eventId, ScaleEvent scaleEvent)
        {
            var scaleGroupDefinition = await GetScaleGroupDefinition(scaleGroup);

            if (scaleEvent.RegionConfig.Select(r => r.Name).Except(scaleGroupDefinition.Regions.Select(r => r.RegionName)).Any())
            {
                throw new ScaleEventSaveException("Only configured regions may be used by the scale event.", ScaleEventSaveFailureReason.InvalidRegionName);
            }

            SaveScaleEventResult saveResult;
            var eventsListAccessor = GetScaleEventsStateItem(scaleGroup);
            var scaleEvents = await eventsListAccessor.Get();
            var isEventUpdated = scaleEvents.TryGetValue(eventId, out var registeredEvent);
            var now = _dateTimeProvider.UtcNow;
            var isTooLate = isEventUpdated
                ? registeredEvent.StartScaleDownAt <= now
                : scaleEvent.StartScaleDownAt <= now;
            if (isTooLate)
                throw new ScaleEventSaveException("Can't register scale event in the past.", ScaleEventSaveFailureReason.RegistrationInThePast);

            foreach (var region in scaleEvent.RegionConfig)
            {
                var regionDefintion = scaleGroupDefinition.Regions.Find(x => x.RegionName == region.Name);
                ValidateRegionMaxScale(scaleEvent, scaleEvents.Values, regionDefintion.MaxScale, region);
            }

            foreach (var regionConfig in scaleEvent.RegionConfig)
            {
                var regionScaleEvent = new RegionScaleEvent
                {
                    Id = eventId,
                    Name = scaleEvent.Name,
                    RequiredScaleAt = scaleEvent.RequiredScaleAt,
                    StartScaleDownAt = scaleEvent.StartScaleDownAt,
                    Scale = regionConfig.Scale,
                };
                var scaleManagerActor = GetActor<IScaleManager>(scaleGroup, regionConfig.Name);
                await scaleManagerActor.ScheduleScaleEvent(regionScaleEvent);
            }

            if (scaleGroupDefinition.HasSharedCosmosDb)
            {
                var regionScaleEvent = new RegionScaleEvent
                {
                    Id = eventId,
                    Name = scaleEvent.Name,
                    RequiredScaleAt = scaleEvent.RequiredScaleAt,
                    StartScaleDownAt = scaleEvent.StartScaleDownAt,
                    Scale = scaleEvent.RegionConfig.Max(r => r.Scale),
                };
                var scaleManagerActor = GetActor<IScaleManager>(scaleGroup, ScaleGroupDefinition.SharedCosmosRegion);
                await scaleManagerActor.ScheduleScaleEvent(regionScaleEvent);
            }

            if (isEventUpdated)
            {
                saveResult = registeredEvent.RequiredScaleAt <= now && now < registeredEvent.StartScaleDownAt
                    ? SaveScaleEventResult.ReplacedExecuting
                    : SaveScaleEventResult.ReplacedWaiting;

                registeredEvent.Name = scaleEvent.Name;
                registeredEvent.RequiredScaleAt = scaleEvent.RequiredScaleAt;
                registeredEvent.StartScaleDownAt = scaleEvent.StartScaleDownAt;

                var removedRegions = registeredEvent.Regions.Keys
                    .Except(scaleEvent.RegionConfig.Select(r => r.Name))
                    .Where(r => r != ScaleGroupDefinition.SharedCosmosRegion)
                    .ToList();
                foreach (var regionName in removedRegions)
                {
                    var scaleManagerActor = GetActor<IScaleManager>(scaleGroup, regionName);
                    await scaleManagerActor.DeleteScaleEvent(eventId);
                    registeredEvent.Regions.Remove(regionName);
                }

                foreach (var region in scaleEvent.RegionConfig)
                {
                    registeredEvent.Regions.TryAdd(region.Name, new ScaleEventRegionState
                    {
                        Scale = region.Scale,
                    });

                    var scaleManagerActor = GetActor<IScaleManager>(scaleGroup, region.Name);
                    await scaleManagerActor.ScheduleScaleEvent(new RegionScaleEvent
                    {
                        Id = eventId,
                        Name = scaleEvent.Name,
                        RequiredScaleAt = scaleEvent.RequiredScaleAt,
                        Scale = region.Scale,
                        StartScaleDownAt = scaleEvent.StartScaleDownAt,
                    });
                }
            }
            else
            {
                registeredEvent = new RegisteredScaleEvent
                {
                    Name = scaleEvent.Name,
                    RequiredScaleAt = scaleEvent.RequiredScaleAt,
                    StartScaleDownAt = scaleEvent.StartScaleDownAt,
                    Regions = scaleEvent.RegionConfig.ToDictionary(r => r.Name, r => new ScaleEventRegionState { Scale = r.Scale }),
                };

                if (scaleGroupDefinition.HasSharedCosmosDb)
                {
                    registeredEvent.Regions.Add(ScaleGroupDefinition.SharedCosmosRegion, new ScaleEventRegionState());
                }

                scaleEvents.Add(eventId, registeredEvent);
                saveResult = SaveScaleEventResult.Created;
            }

            await eventsListAccessor.Set(scaleEvents);

            var leadTime = scaleGroupDefinition.MaxLeadTime(scaleEvent.RegionConfig.Select(r => r.Name));
            var scheduledScaleEvent = registeredEvent.ToScheduledScaleEvent(eventId, leadTime);
            return new SaveScaleEventReturnValue { Result = saveResult, ScheduledScaleEvent = scheduledScaleEvent };
        }

        private void ValidateRegionMaxScale(ScaleEvent scaleEvent, IEnumerable<RegisteredScaleEvent> scaleEvents, int? maxScale, RegionScaleValue regionScale)
        {
            if (!maxScale.HasValue)
                return;

            if (maxScale.Value < regionScale.Scale)
            {
                throw new ScaleEventSaveException($"The scale for region {regionScale.Name} exceeds region's maximum scale.",
                    ScaleEventSaveFailureReason.ScaleLimitExceeded);
            }

            var overlappingEvents = scaleEvents
                .Where(x => x.Regions.ContainsKey(regionScale.Name)
                    && x.StartScaleDownAt > scaleEvent.RequiredScaleAt && scaleEvent.StartScaleDownAt > x.RequiredScaleAt)
                .Select(x => new { x.RequiredScaleAt, x.StartScaleDownAt, x.Regions[regionScale.Name].Scale })
                .ToList();
            if (overlappingEvents.Count == 0)
                return;

            var scaleChanges = overlappingEvents.Select(x => (time: x.StartScaleDownAt, scale: -x.Scale))
                .Union(overlappingEvents.Select(x => (time: x.RequiredScaleAt, scale: x.Scale)))
                .GroupBy(x => x.time)
                .Select(x => (time: x.Key, scale: x.Sum(y => y.scale)))
                .OrderBy(x => x.time)
                .Select(x => x.scale);

            var maxAllowedScale = maxScale.Value - regionScale.Scale;
            var accumulatedScale = 0;
            foreach (var scale in scaleChanges)
            {
                accumulatedScale += scale;
                if (accumulatedScale > maxAllowedScale)
                    throw new ScaleEventSaveException($"The scale for region {regionScale.Name} exceeds region's maximum scale.",
                        ScaleEventSaveFailureReason.ScaleLimitExceeded);
            }
        }

        async Task<ScaleEventState> IConfigurationManager.DeleteScaleEvent(string scaleGroup, Guid eventId)
        {
            var definition = await GetScaleGroupDefinition(scaleGroup);
            var scaleEventsState = GetScaleEventsStateItem(scaleGroup);
            var list = await scaleEventsState.Get();
            if (list.TryGetValue(eventId, out var scaleEvent))
            {
                foreach (var region in scaleEvent.Regions)
                {
                    var scaleManagerActor = GetActor<IScaleManager>(scaleGroup, region.Key);
                    await scaleManagerActor.DeleteScaleEvent(eventId);
                }

                if (definition.HasSharedCosmosDb)
                {
                    var scaleManagerActor = GetActor<IScaleManager>(scaleGroup, ScaleGroupDefinition.SharedCosmosRegion);
                    await scaleManagerActor.DeleteScaleEvent(eventId);
                }

                list.Remove(eventId);
                await scaleEventsState.Set(list);

                var now = _dateTimeProvider.UtcNow;
                var regions = from region in scaleEvent.Regions
                              where region.Key != ScaleGroupDefinition.SharedCosmosRegion
                              select region.Key;
                var leadTime = definition.MaxLeadTime(regions);
                if (now < scaleEvent.RequiredScaleAt - leadTime)
                    return ScaleEventState.Waiting;
                return now < scaleEvent.StartScaleDownAt ? ScaleEventState.Executing : ScaleEventState.Completed;
            }
            else
            {
                throw new ScaleEventNotFoundException();
            }
        }

        async Task<ScaleGroupState> IConfigurationManager.GetScaleState(string scaleGroup)
        {
            var definition = await GetScaleGroupDefinition(scaleGroup);

            var scaleRegionStates = new List<ScaleRegionState>();
            foreach (var region in definition.Regions)
            {
                var scaleManagerActor = GetActor<IScaleManager>(scaleGroup, region.RegionName);
                var state = await scaleManagerActor.GetScaleSet();
                if (state != null)
                {
                    scaleRegionStates.Add(new ScaleRegionState
                    {
                        Name = region.RegionName,
                        Scale = state.Scale,
                        RequestedScale = state.RequestedScale,
                        WasScaledUpAt = state.WasScaleUpAt,
                        WillScaleDownAt = state.WillScaleDownAt,
                        ScaleSetState = state.ScaleSetState,
                    });
                }
            }

            return new ScaleGroupState
            {
                Regions = scaleRegionStates,
            };
        }

        async Task IConfigurationManager.ReportScaleEventState(string scaleGroup, string region, List<ScaleEventStateChange> changes)
        {
            var stateItem = GetScaleEventsStateItem(scaleGroup);
            var scaleEvents = await stateItem.Get();
            foreach (var change in changes)
            {
                if (scaleEvents.TryGetValue(change.EventId, out var scaleEvent))
                {
                    scaleEvent.Regions[region].State = change.State;
                    ReportEventStateChange(scaleGroup, change.EventId, scaleEvent);
                }
            }

            await stateItem.Set(scaleEvents);
        }

        private void ReportEventStateChange(string scaleGroup, Guid eventId, RegisteredScaleEvent scaleEvent)
        {
            var currentState = scaleEvent.CurrentState;
            if (currentState == ScaleChangeType.Waiting)
                return;
            if (currentState == scaleEvent.ReportedState)
                return;

            BigBrother.Publish(new ScaleChange
            {
                Id = eventId,
                Type = currentState,
                ScaleGroup = scaleGroup,
            });

            scaleEvent.ReportedState = currentState;
        }

        private StateItem<ScaleGroupDefinition> GetScaleGroupState(string name)
        {
            return new StateItem<ScaleGroupDefinition>(StateManager, ScaleGroupKeyPrefix + name);
        }

        private StateItem<Dictionary<Guid, RegisteredScaleEvent>> GetScaleEventsStateItem(string scaleGroup)
        {
            return new StateItem<Dictionary<Guid, RegisteredScaleEvent>>(StateManager, EventsListKeyPrefix + scaleGroup);
        }

        private async Task<ScaleGroupDefinition> GetScaleGroupDefinition(string name)
        {
            var state = GetScaleGroupState(name);
            var configuration = await state.TryGet();
            if (configuration.HasValue)
                return configuration.Value;

            throw new ScaleGroupNotFoundException($"The scale group {name} has not been defined.");
        }

        private async Task DisableRegions(string name, IEnumerable<string> regions, bool disableCosmos)
        {
            if (disableCosmos)
                regions = regions.Union(new[] { ScaleGroupDefinition.SharedCosmosRegion });
            foreach (var region in regions)
            {
                var actor = GetActor<IScaleManager>(name, region);
                await actor.Disable();
            }
        }

        private async Task UpdateScaleManagers(string name, ScaleGroupDefinition definition, Microsoft.ServiceFabric.Data.ConditionalValue<ScaleGroupDefinition> existingGroup)
        {
            foreach (var region in definition.Regions)
            {
                var actor = GetActor<IScaleManager>(name, region.RegionName);
                var configuration = new ScaleManagerConfiguration
                {
                    ScaleSetConfigurations = region.ScaleSets,
                    CosmosConfigurations = region.Cosmos,
                    CosmosDbPrescaleLeadTime = region.CosmosDbPrescaleLeadTime,
                    ScaleSetPrescaleLeadTime = region.ScaleSetPrescaleLeadTime,
                };
                await actor.Configure(configuration);
            }

            if (definition.HasSharedCosmosDb)
            {
                var actor = GetActor<IScaleManager>(name, ScaleGroupDefinition.SharedCosmosRegion);
                var configuration = new ScaleManagerConfiguration
                {
                    ScaleSetConfigurations = new List<ScaleSetConfiguration>(),
                    CosmosConfigurations = definition.Cosmos,
                    CosmosDbPrescaleLeadTime = definition.CosmosDbPrescaleLeadTime,
                };
                await actor.Configure(configuration);
            }

            var existingRegions = existingGroup.HasValue ? existingGroup.Value.Regions : new List<ScaleGroupRegion>();
            var removedRegions = existingRegions
                .Where(rg => !definition.Regions.Any(r => r.RegionName == rg.RegionName))
                .Select(rg => rg.RegionName);
            var disableCosmos = existingGroup.HasValue && existingGroup.Value.HasSharedCosmosDb
                && !definition.HasSharedCosmosDb;
            await DisableRegions(name, removedRegions, disableCosmos);
        }

        private TActor GetActor<TActor>(string scaleGroup, string region)
               where TActor : IActor
        {
            var actorName = typeof(TActor).Name;
            if (actorName.StartsWith('I'))
                actorName = actorName.Substring(1);
            var actorId = new ActorId($"{actorName}:{scaleGroup}/{region}");
            return _proxyFactory.CreateActorProxy<TActor>(actorId);
        }
    }
}
