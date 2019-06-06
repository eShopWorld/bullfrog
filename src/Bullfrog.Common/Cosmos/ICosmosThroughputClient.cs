using System;
using System.Threading.Tasks;

namespace Bullfrog.Common.Cosmos
{
    public interface ICosmosThroughputClient : IDisposable
    {
        Task<CosmosThroughput> Get();

        Task<CosmosThroughput> Set(int throughput);
    }
}
