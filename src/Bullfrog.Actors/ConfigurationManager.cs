using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bullfrog.Actors.Interfaces;
using Bullfrog.Actors.Interfaces.Models;
using Microsoft.ServiceFabric.Actors;
using Microsoft.ServiceFabric.Actors.Client;
using Microsoft.ServiceFabric.Actors.Runtime;

namespace Bullfrog.Actors
{
    [StatePersistence(StatePersistence.Persisted)]
    public class ConfigurationManager : Actor, IConfigurationManager
    {
        private const string ScaleGroupKeyPrefix = "scaleGroup:";
        private readonly IActorProxyFactory _proxyFactory;

        /// <summary>
        /// Initializes a new instance of ScaleManager
        /// </summary>
        /// <param name="actorService">The Microsoft.ServiceFabric.Actors.Runtime.ActorService that will host this actor instance.</param>
        /// <param name="actorId">The Microsoft.ServiceFabric.Actors.ActorId for this actor instance.</param>
        /// <param name="proxyFactory">The factory of actor client proxies.</param>
        public ConfigurationManager(ActorService actorService, ActorId actorId, IActorProxyFactory proxyFactory)
            : base(actorService, actorId)
        {
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
        }

        async Task<List<string>> IConfigurationManager.ListConfiguredScaleGroup(CancellationToken cancellationToken)
        {
            var names = await StateManager.GetStateNamesAsync(cancellationToken);
            return names
                .Where(n => n.StartsWith(ScaleGroupKeyPrefix))
                .Select(n => n.Substring(ScaleGroupKeyPrefix.Length))
                .ToList();
        }

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

        private StateItem<ScaleGroupDefinition> GetScaleGroupState(string name)
        {
            return new StateItem<ScaleGroupDefinition>(StateManager, ScaleGroupKeyPrefix + name);
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
