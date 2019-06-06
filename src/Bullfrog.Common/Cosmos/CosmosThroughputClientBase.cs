using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace Bullfrog.Common.Cosmos
{
    internal abstract class CosmosThroughputClientBase : ICosmosThroughputClient
    {
        public abstract Task<CosmosThroughput> Get();
        public abstract Task<CosmosThroughput> Set(int throughput);

        #region IDisposable Support

        protected virtual void Dispose(bool disposing)
        {
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
        }
        #endregion
    }

}
