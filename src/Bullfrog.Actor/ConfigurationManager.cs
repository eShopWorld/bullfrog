namespace Bullfrog.Actors
{
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

    [StatePersistence(StatePersistence.Persisted)]
    public class ConfigurationManager : Actor, IConfigurationManager
    {
        private const string ScaleGroupKeyPrefix = "scaleGroup:";

        /// <summary>
        /// Initializes a new instance of ScaleManager
        /// </summary>
        /// <param name="actorService">The Microsoft.ServiceFabric.Actors.Runtime.ActorService that will host this actor instance.</param>
        /// <param name="actorId">The Microsoft.ServiceFabric.Actors.ActorId for this actor instance.</param>
        public ConfigurationManager(ActorService actorService, ActorId actorId)
            : base(actorService, actorId)
        {
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

        async Task<Dictionary<string, string[]>> IConfigurationManager.ConfigureScaleGroup(string name, ScaleGroupDefinition definition, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(name) || name.Trim() != name)
            {
                throw new ArgumentException("The name parameter is invalid", nameof(name));
            }

            var state = GetScaleGroupState(name);

            var existingGroups = await state.TryGet(cancellationToken);

            if (definition == null)
            {
                // Delete the scale group if it has been registered.
                if (existingGroups.HasValue)
                {
                    await DeleteScaleGroup(name, existingGroups.Value);
                    await state.Remove(default);
                }

                return null;
            }

            await UpdateScaleManagers(name, definition, existingGroups);

            await state.Set(definition, default);
            return null;
        }

        async Task<List<string>> IConfigurationManager.ListConfiguredScaleGroup(CancellationToken cancellationToken)
        {
            var names = await StateManager.GetStateNamesAsync(cancellationToken);
            return names
                .Where(n => n.StartsWith(ScaleGroupKeyPrefix))
                .Select(n => n.Substring(ScaleGroupKeyPrefix.Length))
                .ToList();
        }

        private StateItem<ScaleGroupDefinition> GetScaleGroupState(string name)
        {
            return new StateItem<ScaleGroupDefinition>(StateManager, ScaleGroupKeyPrefix + name);
        }

        private async Task DeleteScaleGroup(string name, ScaleGroupDefinition existingGroup)
        {
            foreach (var region in existingGroup.Regions)
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
                    ScaleSetConfiguration = region.ScaleSet,
                    CosmosConfigurations = region.Cosmos,
                };
                await actor.Configure(configuration, default);
            }

            var existingRegions = existingGroup.HasValue ? existingGroup.Value.Regions : new List<ScaleGroupRegion>();
            var removedRegions = existingRegions.Where(rg => !definition.Regions.Any(r => r.RegionName == rg.RegionName));
            foreach (var removedRegion in removedRegions)
            {
                var actor = GetActor<IScaleManager>(name, removedRegion.RegionName);
                await actor.Disable(default);
            }
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

        protected TActor GetActor<TActor>(string scaleGroup, string region)
               where TActor : IActor
        {
            var actorName = typeof(TActor).Name;
            if (actorName.StartsWith('I'))
                actorName = actorName.Substring(1);
            var actorId = new ActorId($"{actorName}:{scaleGroup}/{region}");
            return ActorProxy.Create<TActor>(actorId);
        }
    }
}
