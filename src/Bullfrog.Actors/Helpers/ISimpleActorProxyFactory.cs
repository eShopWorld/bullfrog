using Microsoft.ServiceFabric.Actors;

namespace Bullfrog.Actors.Helpers
{
    public interface ISimpleActorProxyFactory
    {
        TActor CreateProxy<TActor>(ActorId actorId)
            where TActor : IActor;
    }
}
