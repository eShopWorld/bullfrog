namespace Bullfrog.Api
{
    /// <summary>
    /// Defines STS authorization scopes used by the application.
    /// </summary>
    public static class AuthenticationPolicies
    {
        /// <summary>
        /// The authorization scope representing full access to the application.
        /// </summary>
        public const string AdminScope = "AdminScope";

        /// <summary>
        /// The authorization scope allowing to manage all scale events.
        /// </summary>
        public const string EventsManagerScope = "EventsManagerScope";

        /// <summary>
        /// The authorization scope allowing to read all existing scale events.
        /// </summary>
        public const string EventsReaderScope = "EventsReaderScope";
    }
}
