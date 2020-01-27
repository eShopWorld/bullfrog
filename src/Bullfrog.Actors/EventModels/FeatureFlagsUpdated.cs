using Eshopworld.Core;

namespace Bullfrog.Actors.EventModels
{
    /// <summary>
    /// Reports updates to the feature flags
    /// </summary>
    public class FeatureFlagsUpdated : TelemetryEvent
    {
        /// <summary>
        /// JSON serialized view of feature flags
        /// </summary>
        public string FeatureFlags { get; set; }
    }
}
