using System.Threading.Tasks;

namespace Bullfrog.Actors.Modules
{
    /// <summary>
    /// Represents a module responsible for long running throughput change operations.
    /// </summary>
    public abstract class ScalingModule
    {
        /// <summary>
        /// Attempts to change throughput.
        /// </summary>
        /// <param name="newThroughput">The new throughput.</param>
        /// <returns>The throughput available if the operation completed,
        /// null if scaling is in progress (in which case the method should
        /// be called again after some delay)</returns>
        public abstract Task<int?> SetThroughput(int? newThroughput);
    }
}
