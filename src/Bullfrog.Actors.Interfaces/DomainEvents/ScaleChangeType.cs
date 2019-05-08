namespace Bullfrog.DomainEvents
{
    /// <summary>
    /// Describes what happend to a scale event
    /// </summary>
    public enum ScaleChangeType
    {
        /// <summary>
        /// An initial state of a scale event.
        /// </summary>
        /// <remarks>
        /// It's an internal state and should be not used in domain events.
        /// </remarks>
        Waiting,

        /// <summary>
        /// An error occurred during scale out phase. This event might be followed by other events when the operation can proceeed.
        /// </summary>
        ScaleIssue,

        /// <summary>
        /// The scale out phase has been started.
        /// </summary>
        ScaleOutStarted,

        /// <summary>
        /// The scale out phase has successfully completed.
        /// </summary>
        ScaleOutComplete,

        /// <summary>
        /// The scale event ended and the scale in phase is starting.
        /// </summary>
        ScaleInStarted,

        /// <summary>
        /// The scale in phase has completed.
        /// </summary>
        ScaleInComplete,
    }
}
