using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using Bullfrog.Actors.EventModels;
using Bullfrog.Actors.Interfaces;
using Bullfrog.Actors.Interfaces.Models;
using Bullfrog.Actors.Models;
using Bullfrog.Common;
using Bullfrog.Common.Models;
using Eshopworld.Core;
using Microsoft.ServiceFabric.Actors;
using Microsoft.ServiceFabric.Actors.Client;
using Microsoft.ServiceFabric.Actors.Runtime;
using Newtonsoft.Json;

namespace Bullfrog.Actors
{
    [StatePersistence(StatePersistence.Persisted)]
    public class ConfigurationManager : BullfrogActorBase, IConfigurationManager
    {
        private const string ScaleGroupKeyPrefix = "scaleGroup:";
        private const string EventsListKeyPrefix = "events:";
        private readonly StateItem<FeatureFlagsConfiguration> _featureFlags;
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
            _featureFlags = new StateItem<FeatureFlagsConfiguration>(StateManager, "featureFlags");
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

                await _proxyFactory.GetScaleEventStateReporter(name).ConfigureRegions(definition.AllRegionNames.ToArray());
                await UpdateScaleManagers(name, definition, existingGroups);
                await state.Set(definition, default);
            }
            else if (existingGroups.HasValue)
            {
                // Delete the scale group if it has been registered.
                await DisableRegions(name, existingGroups.Value.AllRegionNames);
                await _proxyFactory.GetScaleEventStateReporter(name).ConfigureRegions(null);
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
                return scaleEvent.ToScheduledScaleEvent(eventId, scaleGroupDefinition.MaxLeadTime(scaleEvent.Regions.Keys));
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
            return list.Select(r => r.Value.ToScheduledScaleEvent(r.Key, scaleGroupDefinition.MaxLeadTime(r.Value.Regions.Keys))).ToList();
        }

        async Task<List<ScheduledScaleEvent>> IConfigurationManager.ListScheduledScaleEvents(string scaleGroup, ListScaleEventsParameters parameters)
        {
            IEnumerable<ScheduledScaleEvent> events;
            var scaleGroupDefinition = await GetScaleGroupDefinition(scaleGroup);
            if (parameters.FromRegion != null)
            {
                if (!scaleGroupDefinition.Regions.Any(r => r.RegionName == parameters.FromRegion))
                    return new List<ScheduledScaleEvent>();
                var scaler = _proxyFactory.GetActor<IScaleManager>(scaleGroup, parameters.FromRegion);
                var storedEvents = await scaler.ListEvents();
                events = storedEvents.Select(ev => new ScheduledScaleEvent
                {
                    Id = ev.Id,
                    Name = ev.Name,
                    RegionConfig = new List<RegionScaleValue>
                    {
                        new RegionScaleValue
                        {
                            Name = parameters.FromRegion,
                            Scale    =  ev.Scale,
                        },
                    },
                    RequiredScaleAt = ev.RequiredScaleAt,
                    StartScaleDownAt = ev.StartScaleDownAt,
                });
            }
            else
            {
                var scaleEventsState = GetScaleEventsStateItem(scaleGroup);
                var list = await scaleEventsState.Get();
                events = list.Select(r => r.Value.ToScheduledScaleEvent(r.Key, scaleGroupDefinition.MaxLeadTime(r.Value.Regions.Keys)));
            }

            if (parameters.ActiveOnly)
                events = events.Where(ev => ev.StartScaleDownAt >= _dateTimeProvider.UtcNow);
            return events.ToList();
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

            if (isEventUpdated && registeredEvent.StartScaleDownAt <= now)
                throw new ScaleEventSaveException("A processed scale event he already processed scale event.", ScaleEventSaveFailureReason.RegistrationInThePast);

            if (scaleEvent.StartScaleDownAt <= now)
                throw new ScaleEventSaveException("Can't register a scale event that ends in the past.", ScaleEventSaveFailureReason.RegistrationInThePast);

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
                var scaleManagerActor = _proxyFactory.GetActor<IScaleManager>(scaleGroup, regionConfig.Name);
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
                var scaleManagerActor = _proxyFactory.GetActor<IScaleManager>(scaleGroup, ScaleGroupDefinition.SharedCosmosRegion);
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
                    var scaleManagerActor = _proxyFactory.GetActor<IScaleManager>(scaleGroup, regionName);
                    await scaleManagerActor.DeleteScaleEvent(eventId);
                    registeredEvent.Regions.Remove(regionName);
                }

                foreach (var region in scaleEvent.RegionConfig)
                {
                    if (registeredEvent.Regions.TryGetValue(region.Name, out var regionState))
                    {
                        regionState.Scale = region.Scale;
                    }
                    else
                    {
                        registeredEvent.Regions.Add(region.Name, new ScaleEventRegionState
                        {
                            Scale = region.Scale,
                        });
                    }
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

            if (scaleGroupDefinition.OldEventsAge.HasValue)
                await RemoveOldScaleEvents(scaleGroup, scaleGroupDefinition.AllRegionNames, scaleEvents, now.Add(-scaleGroupDefinition.OldEventsAge.Value));

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
                await DeleteEventsFromOtherActors(scaleGroup, definition.AllRegionNames, new[] { eventId });

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
            foreach (var region in definition.AllRegionNames)
            {
                var scaleManagerActor = _proxyFactory.GetActor<IScaleManager>(scaleGroup, region);
                var state = await scaleManagerActor.GetScaleSet();
                if (state != null)
                {
                    scaleRegionStates.Add(new ScaleRegionState
                    {
                        Name = region,
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

        async Task<FeatureFlagsConfiguration> IConfigurationManager.GetFeatureFlags()
        {
            return (await _featureFlags.TryGet()).Value ?? new FeatureFlagsConfiguration();
        }

        async Task<FeatureFlagsConfiguration> IConfigurationManager.SetFeatureFlags(FeatureFlagsConfiguration featureFlags)
        {
            await _featureFlags.Set(featureFlags);
            BigBrother.Publish(new FeatureFlagsUpdated
            {
                FeatureFlags = JsonConvert.SerializeObject(featureFlags),
            });
            return featureFlags;
        }

        private StateItem<ScaleGroupDefinition> GetScaleGroupState(string name)
        {
            return new StateItem<ScaleGroupDefinition>(StateManager, ScaleGroupKeyPrefix + name);
        }

        private async Task RemoveOldScaleEvents(string scaleGroup, IEnumerable<string> regionNames, Dictionary<Guid, RegisteredScaleEvent> events, DateTimeOffset completedBefore)
        {
            var oldEvents = events.Where(kv => kv.Value.StartScaleDownAt < completedBefore).ToList();
            if (oldEvents.Count > 0)
            {
                await DeleteEventsFromOtherActors(scaleGroup, regionNames, oldEvents.Select(x => x.Key));

                foreach (var e in oldEvents)
                {
                    events.Remove(e.Key);
                    BigBrother.Publish(new PurgingScaleEvent
                    {
                        ScaleEventId = e.Key,
                        Name = e.Value.Name,
                        RegionsSummary = string.Join("; ", e.Value.Regions.Select(x => $"{x.Key}={x.Value.Scale}")),
                        RequiredScaleAt = e.Value.RequiredScaleAt,
                        StartScaleDownAt = e.Value.StartScaleDownAt,
                    });
                }
            }
        }

        private async Task DeleteEventsFromOtherActors(string scaleGroup, IEnumerable<string> regionNames, IEnumerable<Guid> events)
        {
            var idsToRemove = events.ToList();
            foreach (var region in regionNames)
                await _proxyFactory.GetActor<IScaleManager>(scaleGroup, region).PurgeScaleEvents(idsToRemove);

            await _proxyFactory.GetScaleEventStateReporter(scaleGroup).PurgeScaleEvents(idsToRemove);
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

        private async Task DisableRegions(string name, IEnumerable<string> regions)
        {
            foreach (var region in regions)
            {
                var actor = _proxyFactory.GetActor<IScaleManager>(name, region);
                await actor.Disable();
            }
        }

        private async Task UpdateScaleManagers(string name, ScaleGroupDefinition definition, Microsoft.ServiceFabric.Data.ConditionalValue<ScaleGroupDefinition> existingGroup)
        {
            var featureFlags = (await _featureFlags.TryGet()).Value ?? new FeatureFlagsConfiguration();

            foreach (var region in definition.Regions)
            {
                var actor = _proxyFactory.GetActor<IScaleManager>(name, region.RegionName);
                var configuration = new ScaleManagerConfiguration
                {
                    ScaleSetConfigurations = region.ScaleSets,
                    CosmosConfigurations = region.Cosmos,
                    CosmosDbPrescaleLeadTime = region.CosmosDbPrescaleLeadTime,
                    ScaleSetPrescaleLeadTime = region.ScaleSetPrescaleLeadTime,
                    AutomationAccounts = definition.AutomationAccounts,
                };
                await actor.Configure(configuration, featureFlags);
            }

            if (definition.HasSharedCosmosDb)
            {
                var actor = _proxyFactory.GetActor<IScaleManager>(name, ScaleGroupDefinition.SharedCosmosRegion);
                var configuration = new ScaleManagerConfiguration
                {
                    ScaleSetConfigurations = new List<ScaleSetConfiguration>(),
                    CosmosConfigurations = definition.Cosmos,
                    CosmosDbPrescaleLeadTime = definition.CosmosDbPrescaleLeadTime,
                };
                await actor.Configure(configuration, featureFlags);
            }

            if (existingGroup.HasValue)
                await DisableRegions(name, existingGroup.Value.AllRegionNames.Except(definition.AllRegionNames));
        }
    }
}
