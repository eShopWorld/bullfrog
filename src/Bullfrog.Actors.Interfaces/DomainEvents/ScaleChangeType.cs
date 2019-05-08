namespace Bullfrog.DomainEvents
{
    /// <summary>
    /// Describes what happend to a scale event
    /// </summary>
    public enum ScaleChangeType
    {
        /// <summary>
        /// An error occurred during scale out phase. This event might be followed by other events when the operation can proceeed.
        /// </summary>
        ScaleIssue = 1,

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
