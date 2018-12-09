using System;
using System.Collections.Generic;
using System.Fabric;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.ServiceFabric.Actors;
using Microsoft.ServiceFabric.Actors.Client;

namespace Bullfrog.Api.Controllers
{
    [AllowAnonymous]
    public abstract class BaseManagementController : ControllerBase
    {
        private static Dictionary<Type, string> actorServices = new Dictionary<Type, string>
        {
            [typeof(Actor.Interfaces.IScaleManager)] = "ScaleManagerActorService",
        };

        internal StatelessServiceContext StatelessServiceContext { get; private set; }

        public BaseManagementController(StatelessServiceContext statelessServiceContext)
        {
            StatelessServiceContext = statelessServiceContext;
        }

        protected TActor GetActor<TActor>(string scaleGroup, string region)
            where TActor : IActor
        {
            var actorName = typeof(TActor).Name;
            if (actorName.StartsWith('I'))
                actorName = actorName.Substring(1);
            var actorId = new ActorId($"{actorName}:{scaleGroup}/{region}");
            var serviceName = actorServices[typeof(TActor)];
            var actorUri = new Uri($"{StatelessServiceContext.CodePackageActivationContext.ApplicationName}/{serviceName}");
            return ActorProxy.Create<TActor>(actorId, actorUri);
        }

        protected string[] ListRegionsOfScaleGroup(string scaleGroup)
        {
            switch (scaleGroup)
            {
                case "SNKRS":
                    return new[] { "us", "eu" };

                case "ONE":
                    return new[] { "eu" };

                default:
                    throw new ArgumentOutOfRangeException(nameof(scaleGroup), scaleGroup, $"The scale group {scaleGroup} is unrecognized.");
            }
        }
    }
}
