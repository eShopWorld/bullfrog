using System;
using System.Collections.Generic;
using System.Fabric;
using System.Linq;
using System.Threading.Tasks;
using Bullfrog.Actors.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.ServiceFabric.Actors;
using Microsoft.ServiceFabric.Actors.Client;

namespace Bullfrog.Api.Controllers
{
    /// <summary>
    /// Provides common functionality for all API controller classes.
    /// </summary>
    [Authorize]
    [ApiController]
    public abstract class BaseManagementController : ControllerBase
    {
        private static readonly Dictionary<Type, string> actorServices = new Dictionary<Type, string>
        {
            [typeof(IScaleManager)] = "ScaleManagerActorService",
            [typeof(IConfigurationManager)] = "ConfigurationManagerActorService",
        };
        private readonly IActorProxyFactory _proxyFactory;

        /// <summary>
        /// Gets the stateless service context.
        /// </summary>
        protected StatelessServiceContext StatelessServiceContext { get; private set; }

        /// <summary>
        /// Creates an instance of <see cref="BaseManagementController"/>.
        /// </summary>
        /// <param name="statelessServiceContext">The <see cref="StatelessServiceContext"/> instance.</param>
        /// <param name="proxyFactory"></param>
        protected BaseManagementController(StatelessServiceContext statelessServiceContext,
            IActorProxyFactory proxyFactory)
        {
            StatelessServiceContext = statelessServiceContext;
            _proxyFactory = proxyFactory;
        }

        /// <summary>
        /// Gets the actor with the specified ID.
        /// </summary>
        /// <typeparam name="TActor">The type of the actor.</typeparam>
        /// <param name="actorId">The ID of the actor.</param>
        /// <returns>The actor proxy.</returns>
        protected TActor GetActor<TActor>(ActorId actorId)
            where TActor : IActor
        {
            var serviceName = actorServices[typeof(TActor)];
            var actorUri = new Uri($"{StatelessServiceContext.CodePackageActivationContext.ApplicationName}/{serviceName}");
            return _proxyFactory.CreateActorProxy<TActor>(actorUri, actorId);
        }

        /// <summary>
        /// Returns the actor responsible for managing region of the scale group.
        /// </summary>
        /// <typeparam name="TActor">The type of the actor.</typeparam>
        /// <param name="scaleGroup">The scale group name.</param>
        /// <param name="region">The region name.</param>
        /// <returns>The actor proxy.</returns>
        protected TActor GetActor<TActor>(string scaleGroup, string region)
            where TActor : IActor
        {
            var actorName = typeof(TActor).Name;
            if (actorName.StartsWith('I'))
                actorName = actorName.Substring(1);
            var actorId = new ActorId($"{actorName}:{scaleGroup}/{region}");
            return GetActor<TActor>(actorId);
        }

        /// <summary>
        /// Returns the list of regions of the specified scale groups.
        /// </summary>
        /// <param name="scaleGroup">The name of the scale group</param>
        /// <returns>The list of region names or null if the scale group is not configured.</returns>
        protected async Task<List<string>> ListRegionsOfScaleGroup(string scaleGroup)
        {
            var actor = GetActor<IConfigurationManager>(new ActorId("configuration"));
            // TODO: caching would be nice
            var configuration = await actor.GetScaleGroupConfiguration(scaleGroup, default);
            return configuration?.Regions.Select(r => r.RegionName).ToList();
        }
    }
}

