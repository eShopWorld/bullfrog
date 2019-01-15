using System.Threading;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Actors.Runtime;
using Microsoft.ServiceFabric.Data;

namespace Bullfrog.Actor
{
    public struct StateItem<T>
    {
        private readonly IActorStateManager _stateManager;

        public string Name { get; }

        public StateItem(IActorStateManager stateManager, string name)
        {
            _stateManager = stateManager;
            Name = name;
        }

        public Task<T> Get(CancellationToken cancellationToken = default)
        {
            return _stateManager.GetStateAsync<T>(Name, cancellationToken);
        }

        public Task Set(T state, CancellationToken cancellationToken = default)
        {
            return _stateManager.SetStateAsync<T>(Name, state, cancellationToken);
        }

        public Task<bool> TryAdd(T value, CancellationToken cancellationToken = default)
        {
            return _stateManager.TryAddStateAsync(Name, value, cancellationToken);
        }

        public Task<ConditionalValue<T>> TryGet(CancellationToken cancellationToken = default)
        {
            return _stateManager.TryGetStateAsync<T>(Name, cancellationToken);
        }

        public Task Remove(CancellationToken cancellationToken = default)
        {
            return _stateManager.RemoveStateAsync(Name, cancellationToken);
        }

        public Task<bool> TryRemove(CancellationToken cancellationToken = default)
        {
            return _stateManager.TryRemoveStateAsync(Name, cancellationToken);
        }
    }
}
