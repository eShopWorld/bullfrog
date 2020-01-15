using System;
using System.Threading.Tasks;
using Bullfrog.Common;
using Eshopworld.Core;

namespace Bullfrog.Actors.ResourceScalers
{
    /// <summary>
    /// Represents a module responsible for long running throughput change operations.
    /// </summary>
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

        /// <summary>
        /// Optional state of the scaler.
        /// </summary>
        public virtual string SerializedState { get; set; }

        /// <summary>
        /// Performs an operation and saves the updated state.
        /// </summary>
        /// <typeparam name="TState">The type of the state.</typeparam>
        /// <typeparam name="TResult">The type of the operation's result.</typeparam>
        /// <param name="operation">The operation to perform.</param>
        /// <returns>Returns a result of operation.</returns>
        protected async Task<TResult> PerformOperationWithState<TState, TResult>(Func<TState, Task<TResult>> operation, IBigBrother bigBrother)
            where TState : class
        {
            TState state = null;
            if (SerializedState == null)
            {
                SerializedState = "{}";
            }

            try
            {
                state = Newtonsoft.Json.JsonConvert.DeserializeObject<TState>(SerializedState);
            }
            catch(Exception ex)
            {
                var descriptionException = new BullfrogException($"Failed to deserialize the resource scaler's state: {SerializedState}.", ex);
                bigBrother.Publish(descriptionException.ToExceptionEvent());
            }

            var result = await operation(state);
            SerializedState = Newtonsoft.Json.JsonConvert.SerializeObject(state);
            return result;
        }
    }
}
