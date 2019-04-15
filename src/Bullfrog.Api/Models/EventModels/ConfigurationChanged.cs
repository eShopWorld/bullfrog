using Eshopworld.Core;

namespace Bullfrog.Api.Models.EventModels
{
    /// <summary>
    /// The event that reports a successful change of configuration of a scale group
    /// </summary>
    public class ConfigurationChanged : TelemetryEvent
    {
        /// <summary>
        /// The name of the updated scale group.
        /// </summary>
        public string ScaleGroup { get; set; }
    }
}
