using Bullfrog.Actors.Interfaces;
using Microsoft.ServiceFabric.Actors;
using Microsoft.ServiceFabric.Actors.Client;

namespace Bullfrog.Actors
{
    internal static class ActorProxyFactoryExtensions
    {
        public static IScaleEventStateReporter GetScaleEventStateReporter(this IActorProxyFactory proxyFactory, string scaleGroup)
        {
            return proxyFactory.CreateActorProxy<IScaleEventStateReporter>(new ActorId("reporter:" + scaleGroup));
        }

        public static TActor GetActor<TActor>(this IActorProxyFactory proxyFactory, string scaleGroup, string region)
               where TActor : IActor
        {
            var actorName = typeof(TActor).Name;
            if (actorName.StartsWith('I'))
                actorName = actorName.Substring(1);
            var actorId = new ActorId($"{actorName}:{scaleGroup}/{region}");
            return proxyFactory.CreateActorProxy<TActor>(actorId);
        }

        public static IRunbookVmssScalingManager GetRunbookVmssScalingManager(this IActorProxyFactory proxyFactory, string scaleGroup, string region, string name)
        {
            return proxyFactory.CreateActorProxy<IRunbookVmssScalingManager>(new ActorId($"runbook:{scaleGroup}/{region}/{name}"));
        }
    }
}
