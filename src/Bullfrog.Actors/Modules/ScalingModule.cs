using System;
using System.Threading.Tasks;

namespace Bullfrog.Actors.Modules
{
    /// <summary>
    /// Represents a module responsible for long running throughput change operations.
    /// </summary>
    abstract class ScalingModule
    {
        /// <summary>
        /// The event raised when the requested throughput has been reached.
        /// </summary>
        /// <remarks>
        /// The <see cref="ReceiveReminderAsync"/> method implementation is responsible for raising the event.
        /// </remarks>
        public event EventHandler<ScaleChangedEventArgs> ScaleChanged;

        public event EventHandler<ScaleChangeFailedEventArgs> ScaleChangeFailed;

        /// <summary>
        /// Executes the periodic action of the module 
        /// </summary>
        /// <returns></returns>
        public abstract Task ReceiveReminderAsync();

        /// <summary>
        /// Resets the throughput to the lowest level.
        /// </summary>
        /// <returns>The new throughput or null if the operation has not completed. The <see cref="ScaleChanged"/> event will be raised </returns>
        public abstract Task<int?> ResetThroughput();

        /// <summary>
        /// Sets the required throughput to the 
        /// </summary>
        /// <param name="throughput"></param>
        /// <returns></returns>
        public abstract Task<int?> SetThroughput(int throughput);

        protected void PublishScaleChangedEvent(ScaleChangedEventArgs eventArgs)
        {
            ScaleChanged?.Invoke(this, eventArgs);
        }

        protected void PublishScaleChangeFailedEvent(ScaleChangeFailedEventArgs eventArgs)
        {
            ScaleChangeFailed?.Invoke(this, eventArgs);
        }
    }
}
