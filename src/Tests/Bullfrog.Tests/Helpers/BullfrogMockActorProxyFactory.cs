using System;
using Castle.DynamicProxy;
using Microsoft.ServiceFabric.Actors;
using Microsoft.ServiceFabric.Actors.Client;

namespace Helpers
{
    /// <summary>
    /// Fixes the mock actor proxy factory by generating proxies which throws exceptions the way the real proxies do.
    /// </summary>
    /// <remarks>
    /// The real actor proxy always throws AggregateException. The MockActorProxyFactory returns objects directly and exceptions throw
    /// are received by a client without any changes.
    /// </remarks>
    internal class BullfrogMockActorProxyFactory : IActorProxyFactory
    {
        private readonly IActorProxyFactory _actorProxyFactory;
        private readonly ProxyGenerator _generator = new ProxyGenerator();

        public BullfrogMockActorProxyFactory(IActorProxyFactory actorProxyFactory)
        {
            _actorProxyFactory = actorProxyFactory;
        }

        TActorInterface IActorProxyFactory.CreateActorProxy<TActorInterface>(ActorId actorId, string applicationName, string serviceName, string listenerName)
        {
            var actor = _actorProxyFactory.CreateActorProxy<TActorInterface>(actorId, applicationName, serviceName, listenerName);

            return _generator.CreateExceptionTransformationInterfaceProxy<TActorInterface>(actor);
        }

        TActorInterface IActorProxyFactory.CreateActorProxy<TActorInterface>(Uri serviceUri, ActorId actorId, string listenerName)
        {
            var actor = _actorProxyFactory.CreateActorProxy<TActorInterface>(serviceUri, actorId, listenerName);

            return _generator.CreateExceptionTransformationInterfaceProxy<TActorInterface>(actor);
        }

        TServiceInterface IActorProxyFactory.CreateActorServiceProxy<TServiceInterface>(Uri serviceUri, ActorId actorId, string listenerName)
        {
            throw new NotImplementedException();
        }

        TServiceInterface IActorProxyFactory.CreateActorServiceProxy<TServiceInterface>(Uri serviceUri, long partitionKey, string listenerName)
        {
            throw new NotImplementedException();
        }
    }
}
