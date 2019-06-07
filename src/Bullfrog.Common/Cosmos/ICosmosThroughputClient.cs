using System;
using System.Threading.Tasks;

namespace Bullfrog.Common.Cosmos
{
    public interface ICosmosThroughputClient
    {
        Task<CosmosThroughput> Get();

        Task<CosmosThroughput> Set(int throughput);
    }
}
