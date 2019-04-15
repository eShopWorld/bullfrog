using System.Threading.Tasks;
using Bullfrog.Actors.EventModels;
using Eshopworld.Core;
using Microsoft.ServiceFabric.Actors;
using Microsoft.ServiceFabric.Actors.Runtime;

namespace Bullfrog.Actors
{
    /// <summary>
    /// Provides a common functionality for actors.
    /// </summary>
    public abstract class BullfrogActorBase : Actor
    {
        private ActorMethodDuration _actorMethodDurationEvent;

        protected IBigBrother BigBrother { get; }

        protected BullfrogActorBase(ActorService actorService, ActorId actorId, IBigBrother bigBrother)
            : base(actorService, actorId)
        {
            BigBrother = bigBrother;
        }

        protected override Task OnPreActorMethodAsync(ActorMethodContext actorMethodContext)
        {
            var reference = ActorReference.Get(this);
            _actorMethodDurationEvent = new ActorMethodDuration
            {
                ActorId = reference.ActorId.ToString(),
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
    }
}
