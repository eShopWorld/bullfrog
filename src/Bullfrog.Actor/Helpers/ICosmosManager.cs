using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Bullfrog.Actor.Interfaces.Models;

namespace Bullfrog.Actor.Helpers
{
    public interface ICosmosManager
    {
        Task<int> SetScale(int requestedScale, CosmosConfiguration configuration, CancellationToken cancellationToken = default);

        Task<int> Reset(CosmosConfiguration configuration, CancellationToken cancellationToken = default);
    }
}
