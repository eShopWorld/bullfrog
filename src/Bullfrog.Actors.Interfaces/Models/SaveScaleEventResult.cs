namespace Bullfrog.Actors.Interfaces.Models
{
    /// <summary>
    /// Describes the result of a scale event save operation.
    /// </summary>
    public enum SaveScaleEventResult
    {
        /// <summary>
        /// A new scale event has been saved.
        /// </summary>
        Created,

        /// <summary>
        /// A currently executing event has been updated.
        /// </summary>
        ReplacedExecuting,

        /// <summary>
        /// A scale event waiting for an execution has been updated.
        /// </summary>
        ReplacedWaiting,
    }
}
