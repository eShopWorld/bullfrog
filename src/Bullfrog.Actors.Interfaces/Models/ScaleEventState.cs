namespace Bullfrog.Actors.Interfaces.Models
{
    /// <summary>
    /// The current state of the scale event.
    /// </summary>
    public enum ScaleEventState
    {
        /// <summary>
        /// The scale event has not started yet.
        /// </summary>
        Waiting,

        /// <summary>
        /// The scale event is now executing.
        /// </summary>
        Executing,

        /// <summary>
        /// The scale event has finished its execution.
        /// </summary>
        Completed,
    }
}
