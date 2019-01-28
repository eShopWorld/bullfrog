using Microsoft.ServiceFabric.Actors;
using Microsoft.ServiceFabric.Actors.Client;

namespace Bullfrog.Actors.Helpers
{
    class SimpleActorProxyFactory : ISimpleActorProxyFactory
    {
        public TActor CreateProxy<TActor>(ActorId actorId) where TActor : IActor
        {
            return ActorProxy.Create<TActor>(actorId);
        }
    }
}
