using System;
using System.Threading;
using System.Threading.Tasks;

namespace Bullfrog.Actors.Helpers
{
    internal interface ICosmosThroughputManager  : IDisposable
    {
        Task<int> SetThroughput(int throughput, CancellationToken cancellationToken = default);

        Task<CosmosThroughput> GetThroughput(CancellationToken cancellationToken = default);
    }
}
