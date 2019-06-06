using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Actors.Runtime;
using Microsoft.ServiceFabric.Data;

namespace Bullfrog.Actors.Helpers
{
    class StateManagerProxy : IActorStateManager
    {
        private readonly IActorStateManager _stateManager;
        private readonly string _prefix;

        public StateManagerProxy(IActorStateManager stateManager, string prefix)
        {
            _stateManager = stateManager;
            _prefix = prefix;
        }

        public Task<T> AddOrUpdateStateAsync<T>(string stateName, T addValue, Func<string, T, T> updateValueFactory, CancellationToken cancellationToken = default)
        {
            return _stateManager.AddOrUpdateStateAsync(_prefix + stateName, addValue, updateValueFactory, cancellationToken);
        }

        public Task AddStateAsync<T>(string stateName, T value, CancellationToken cancellationToken = default)
        {
            return _stateManager.AddStateAsync(_prefix + stateName, value, cancellationToken);
        }

        public Task ClearCacheAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Clearing cash is not supported");
        }

        public Task<bool> ContainsStateAsync(string stateName, CancellationToken cancellationToken = default)
        {
            return _stateManager.ContainsStateAsync(_prefix + stateName, cancellationToken);
        }

        public Task<T> GetOrAddStateAsync<T>(string stateName, T value, CancellationToken cancellationToken = default)
        {
            return _stateManager.GetOrAddStateAsync(_prefix + stateName, value, cancellationToken);
        }

        public Task<T> GetStateAsync<T>(string stateName, CancellationToken cancellationToken = default)
        {
            return _stateManager.GetStateAsync<T>(_prefix + stateName, cancellationToken);
        }

        public async Task<IEnumerable<string>> GetStateNamesAsync(CancellationToken cancellationToken = default)
        {
            var names = await _stateManager.GetStateNamesAsync(cancellationToken);

            return names.Where(n => n.StartsWith(_prefix, StringComparison.Ordinal)).ToList();
        }

        public Task RemoveStateAsync(string stateName, CancellationToken cancellationToken = default)
        {
            return _stateManager.RemoveStateAsync(_prefix + stateName, cancellationToken);
        }

        public Task SaveStateAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("The SaveStateAsync is not supported");
        }

        public Task SetStateAsync<T>(string stateName, T value, CancellationToken cancellationToken = default)
        {
            return _stateManager.SetStateAsync(_prefix + stateName, value, cancellationToken);
        }

        public Task<bool> TryAddStateAsync<T>(string stateName, T value, CancellationToken cancellationToken = default)
        {
            return _stateManager.TryAddStateAsync(_prefix + stateName, value, cancellationToken);
        }

        public Task<ConditionalValue<T>> TryGetStateAsync<T>(string stateName, CancellationToken cancellationToken = default)
        {
            return _stateManager.TryGetStateAsync<T>(_prefix + stateName, cancellationToken);
        }

        public Task<bool> TryRemoveStateAsync(string stateName, CancellationToken cancellationToken = default)
        {
            return _stateManager.TryRemoveStateAsync(_prefix + stateName, cancellationToken);
        }
    }
}
