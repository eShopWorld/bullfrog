using System;
using System.Collections.Generic;
using System.Fabric;
using System.Linq;
using System.Threading.Tasks;
using Bullfrog.Actor.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.ServiceFabric.Actors;
using Microsoft.ServiceFabric.Actors.Client;

namespace Bullfrog.Api.Controllers
{
    [AllowAnonymous]
    [ApiController]
    public abstract class BaseManagementController : ControllerBase
    {
        private static Dictionary<Type, string> actorServices = new Dictionary<Type, string>
        {
            [typeof(IScaleManager)] = "ScaleManagerActorService",
            [typeof(IConfigurationManager)] = "ConfigurationManagerActorService",
        };

        protected StatelessServiceContext StatelessServiceContext { get; private set; }

        public BaseManagementController(StatelessServiceContext statelessServiceContext)
        {
            StatelessServiceContext = statelessServiceContext;
        }

        protected TActor GetActor<TActor>(ActorId actorId)
            where TActor : IActor
        {
            var serviceName = actorServices[typeof(TActor)];
            var actorUri = new Uri($"{StatelessServiceContext.CodePackageActivationContext.ApplicationName}/{serviceName}");
            return ActorProxy.Create<TActor>(actorId, actorUri);
        }

        protected TActor GetActor<TActor>(string scaleGroup, string region)
            where TActor : IActor
        {
            var actorName = typeof(TActor).Name;
            if (actorName.StartsWith('I'))
                actorName = actorName.Substring(1);
            var actorId = new ActorId($"{actorName}:{scaleGroup}/{region}");
            return GetActor<TActor>(actorId);
        }

        protected async Task<List<string>> ListRegionsOfScaleGroup(string scaleGroup)
        {
            var actor = GetActor<IConfigurationManager>(new ActorId("configuration"));
            // TODO: caching would be nice
            var configuration = await actor.GetScaleGroupConfiguration(scaleGroup, default);
            return configuration?.Regions.Select(r => r.RegionName).ToList();
        }
    }
}

