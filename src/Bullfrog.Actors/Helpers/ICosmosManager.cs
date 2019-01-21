using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Bullfrog.Actors.Interfaces.Models;

namespace Bullfrog.Actors.Helpers
{
    public interface ICosmosManager
    {
        Task<int> SetScale(int requestedScale, CosmosConfiguration configuration, CancellationToken cancellationToken = default);

        Task<int> Reset(CosmosConfiguration configuration, CancellationToken cancellationToken = default);
    }
}
