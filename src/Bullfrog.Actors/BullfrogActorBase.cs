using System.Threading.Tasks;
using Bullfrog.Actors.EventModels;
using Bullfrog.Actors.Interfaces;
using Bullfrog.Common.Telemetry;
using Eshopworld.Core;
using Microsoft.ServiceFabric.Actors;
using Microsoft.ServiceFabric.Actors.Client;
using Microsoft.ServiceFabric.Actors.Runtime;

namespace Bullfrog.Actors
{
    /// <summary>
    /// Provides a common functionality for actors.
    /// </summary>
    public abstract class BullfrogActorBase : Actor
    {
        private ActorMethodDuration _actorMethodDurationEvent;
        private readonly IActorProxyFactory _proxyFactory;

        protected IBigBrother BigBrother { get; }

        protected BullfrogActorBase(ActorService actorService, ActorId actorId, IBigBrother bigBrother, IActorProxyFactory proxyFactory)
            : base(actorService, actorId)
        {
            BigBrother = bigBrother;
            _proxyFactory = proxyFactory;
        }

        protected override Task OnPreActorMethodAsync(ActorMethodContext actorMethodContext)
        {
            LogicalCallTelemetryInitializer.Instance.SetProperty("ActorId", Id.ToString());

            _actorMethodDurationEvent = new ActorMethodDuration
            {
                Name = actorMethodContext.MethodName,
            };
            return base.OnPreActorMethodAsync(actorMethodContext);
        }

        protected override Task OnPostActorMethodAsync(ActorMethodContext actorMethodContext)
        {
            BigBrother.Publish(_actorMethodDurationEvent);
            _actorMethodDurationEvent = null;
            return base.OnPostActorMethodAsync(actorMethodContext);
        }

        protected IScaleEventStateReporter GetScaleEventStateReporter(string scaleGroup)
        {
            return _proxyFactory.CreateActorProxy<IScaleEventStateReporter>(new ActorId("reporter:" + scaleGroup));
        }

        protected TActor GetActor<TActor>(string scaleGroup, string region)
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
