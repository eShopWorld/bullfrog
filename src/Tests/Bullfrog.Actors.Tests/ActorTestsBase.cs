using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Fabric;
using System.Threading;
using Microsoft.ServiceFabric.Actors;
using Microsoft.ServiceFabric.Actors.Client;
using Microsoft.ServiceFabric.Actors.Runtime;
using Microsoft.ServiceFabric.Data;
using Microsoft.ServiceFabric.Services.Remoting;
using Moq;

public abstract class ActorTestsBase<T>
    where T : class, IActor
{
    protected MockRepository MockRepository { get; }

    protected ActorService ActorService { get; }

    protected Mock<IActorStateManager> ActorStateManagerMock { get; }

    private readonly Dictionary<(Type, ActorId), object> _registeredActorProxies
        = new Dictionary<(Type, ActorId), object>();

    protected ActorTestsBase()
    {
        MockRepository = new MockRepository(MockBehavior.Strict);
        var codePackageActivationContextMock = MockRepository.Create<ICodePackageActivationContext>();
        var serviceContext = new StatefulServiceContext(
            new NodeContext("", new NodeId(8, 8), 8, "", ""),
            codePackageActivationContextMock.Object,
            "",
            new Uri("http://tests.local"),
            null,
            Guid.NewGuid(),
            0L);
        ActorStateManagerMock = MockRepository.Create<IActorStateManager>();
        ActorService = new ActorService(serviceContext,
            ActorTypeInformation.Get(typeof(T)),
            stateManagerFactory: (actorBase, stateProvider) => ActorStateManagerMock.Object);
    }

    protected T GetActor()
    {
        return GetActorMock().Object;
    }

    protected Mock<T> GetActorMock()
    {
        return MockRepository.Create<T>(ActorService, new ActorId("conf"), new TestProxyFactory(_registeredActorProxies));
    }

    protected void RegisterActorProxy<TActor>(ActorId actorId, TActor proxy)
        where TActor : IActor
    {
        _registeredActorProxies.Add((typeof(TActor), actorId), proxy);
    }

    protected void AddOptionalState<TItem>(string name, TItem value)
    {
        ActorStateManagerMock.Setup(sm => sm.TryGetStateAsync<TItem>(name, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConditionalValue<TItem>(true, value));
    }

    protected void AddMissingOptionalState<TItem>(string name)
    {
        ActorStateManagerMock.Setup(sm => sm.TryGetStateAsync<TItem>(name, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConditionalValue<TItem>());
    }

    [ExcludeFromCodeCoverage]
    private class TestProxyFactory : IActorProxyFactory
    {
        private readonly Dictionary<(Type, ActorId), object> _registrations;

        public TestProxyFactory(Dictionary<(Type, ActorId), object> registrations)
        {
            _registrations = registrations;
        }

        public TActorInterface CreateActorProxy<TActorInterface>(ActorId actorId, string applicationName = null, string serviceName = null, string listenerName = null) where TActorInterface : IActor
        {
            if (_registrations.TryGetValue((typeof(TActorInterface), actorId), out var proxy))
            {
                return (TActorInterface)proxy;
            }
            else
            {
                throw new Exception($"The proxy for {typeof(TActorInterface).Name} with Id {actorId} has not been registered.");
            }
        }

        public TActorInterface CreateActorProxy<TActorInterface>(Uri serviceUri, ActorId actorId, string listenerName = null) where TActorInterface : IActor
        {
            throw new NotImplementedException();
        }

        public TServiceInterface CreateActorServiceProxy<TServiceInterface>(Uri serviceUri, ActorId actorId, string listenerName = null) where TServiceInterface : IService
        {
            throw new NotImplementedException();
        }

        public TServiceInterface CreateActorServiceProxy<TServiceInterface>(Uri serviceUri, long partitionKey, string listenerName = null) where TServiceInterface : IService
        {
            throw new NotImplementedException();
        }
    }
}
