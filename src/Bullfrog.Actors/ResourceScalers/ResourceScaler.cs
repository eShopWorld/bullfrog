using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;

namespace Bullfrog.Actors.ResourceScalers
{
    /// <summary>
    /// Represents a module responsible for long running throughput change operations.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public abstract class ResourceScaler
    {
        /// <summary>
        /// Starts the scale out operation.
        /// </summary>
        /// <param name="throughput">The requested throughput.</param>
        /// <param name="endsAt">The end scale out period hint.</param>
        /// <returns>The final throughput value or null if the operation has not completed yet.</returns>
        public abstract Task<int?> ScaleOut(int throughput, DateTimeOffset endsAt);

        /// <summary>
        /// Starts the scale in operation.
        /// </summary>
        /// <returns>Returns true if operation has completed or false if the request should be repeated after some delay.</returns>
        public abstract Task<bool> ScaleIn();
    }
}
