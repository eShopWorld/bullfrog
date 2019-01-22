using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Fabric;
using System.Threading;
using Bullfrog.Actors.Helpers;
using Microsoft.ServiceFabric.Actors;
using Microsoft.ServiceFabric.Actors.Runtime;
using Microsoft.ServiceFabric.Data;
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


    private class TestProxyFactory : ISimpleActorProxyFactory
    {
        private readonly Dictionary<(Type, ActorId), object> _registrations;

        public TestProxyFactory(Dictionary<(Type, ActorId), object> registrations)
        {
            _registrations = registrations;
        }

        [ExcludeFromCodeCoverage]
        public TActor CreateProxy<TActor>(ActorId actorId) where TActor : IActor
        {
            if (_registrations.TryGetValue((typeof(TActor), actorId), out var proxy))
            {
                return (TActor)proxy;
            }
            else
            {
                throw new Exception($"The proxy for {typeof(TActor).Name} with Id {actorId} has not been registered.");
            }
        }
    }
}
