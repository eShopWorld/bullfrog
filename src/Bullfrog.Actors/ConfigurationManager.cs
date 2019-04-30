using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bullfrog.Actors.Interfaces;
using Bullfrog.Actors.Interfaces.Models;
using Bullfrog.Actors.Models;
using Bullfrog.Common;
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
        async Task IConfigurationManager.ConfigureScaleGroup(string name, ScaleGroupDefinition definition, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(name) || name.Trim() != name)
            {
                throw new ArgumentException("The name parameter is invalid", nameof(name));
            }

            var state = GetScaleGroupState(name);

            var existingGroups = await state.TryGet(cancellationToken);

            if (definition != null)
            {
                await UpdateScaleManagers(name, definition, existingGroups);
                await state.Set(definition, default);
            }
            else if (existingGroups.HasValue)
            {
                // Delete the scale group if it has been registered.
                await DisableRegions(name, existingGroups.Value.Regions);
                await state.Remove(default);
            }

            var eventsList = GetScaleEventsStateItem(name);
            await eventsList.TryAdd(new Dictionary<Guid, RegisteredScaleEvent>());
        }

        async Task<List<string>> IConfigurationManager.ListConfiguredScaleGroup(CancellationToken cancellationToken)
        {
            var names = await StateManager.GetStateNamesAsync(cancellationToken);
            return names
                .Where(n => n.StartsWith(ScaleGroupKeyPrefix))
                .Select(n => n.Substring(ScaleGroupKeyPrefix.Length))
                .ToList();
        }

        [SuppressMessage("Major Code Smell", "S4457:Parameter validation in \"async\"/\"await\" methods should be wrapped", Justification = "Not necessary")]
        async Task<ScaleGroupDefinition> IConfigurationManager.GetScaleGroupConfiguration(string name, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(name) || name.Trim() != name)
            {
                throw new ArgumentException("The name parameter is invalid", nameof(name));
            }
            var state = GetScaleGroupState(name);
            var configuration = await state.TryGet(cancellationToken);
            return configuration.HasValue ? configuration.Value : null;
        }

        async Task<ScheduledScaleEvent> IConfigurationManager.GetScaleEvent(string scaleGroup, Guid eventId)
        {
            var scaleGroupDefinition = await GetScaleGroupDefinition(scaleGroup);
            var scaleEventsState = GetScaleEventsStateItem(scaleGroup);
            var list = await scaleEventsState.Get();
            if (list.TryGetValue(eventId, out var scaleEvent))
            {
                return scaleEvent.ToScheduledScaleEvent(eventId, scaleGroupDefinition.MaxLeadTime);
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
            var maxLeadTime = scaleGroupDefinition.MaxLeadTime;
            return list.Select(r => r.Value.ToScheduledScaleEvent(r.Key, maxLeadTime)).ToList();
        }

        async Task<(SaveScaleEventResult result, ScheduledScaleEvent scheduledScaleEvent)> IConfigurationManager.SaveScaleEvent(string scaleGroup, Guid eventId, ScaleEvent scaleEvent)
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

            if (isEventUpdated)
            {
                saveResult = registeredEvent.RequiredScaleAt <= now && now < registeredEvent.StartScaleDownAt
                    ? SaveScaleEventResult.ReplacedExecuting
                    : SaveScaleEventResult.ReplacedWaiting;

                registeredEvent.Name = scaleEvent.Name;
                registeredEvent.RequiredScaleAt = scaleEvent.RequiredScaleAt;
                registeredEvent.StartScaleDownAt = scaleEvent.StartScaleDownAt;

                var removedRegions = registeredEvent.Regions.Keys.Except(scaleEvent.RegionConfig.Select(r => r.Name)).ToList();
                foreach (var regionName in removedRegions)
                {
                    var scaleManagerActor = GetActor<IScaleManager>(scaleGroup, regionName);
                    await scaleManagerActor.DeleteScaleEvent(eventId);
                    registeredEvent.Regions.Remove(regionName);
                }

                foreach (var region in scaleEvent.RegionConfig)
                {
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

                scaleEvents.Add(eventId, registeredEvent);
                saveResult = SaveScaleEventResult.Created;
            }

            await eventsListAccessor.Set(scaleEvents);

            return (saveResult, null);
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

                list.Remove(eventId);
                await scaleEventsState.Set(list);

                var now = _dateTimeProvider.UtcNow;
                if (now < scaleEvent.RequiredScaleAt - definition.MaxLeadTime)
                    return ScaleEventState.Waiting;
                return now < scaleEvent.StartScaleDownAt ? ScaleEventState.Executing : ScaleEventState.Completed;
            }
            else
            {
                throw new ScaleEventNotFoundException();
            }
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

        private async Task DisableRegions(string name, IEnumerable<ScaleGroupRegion> regions)
        {
            foreach (var region in regions)
            {
                var actor = GetActor<IScaleManager>(name, region.RegionName);
                await actor.Disable(default);
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
                await actor.Configure(configuration, default);
            }

            var existingRegions = existingGroup.HasValue ? existingGroup.Value.Regions : new List<ScaleGroupRegion>();
            var removedRegions = existingRegions.Where(rg => !definition.Regions.Any(r => r.RegionName == rg.RegionName));
            await DisableRegions(name, removedRegions);
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
