using Eshopworld.Core;

namespace Bullfrog.Api.Models.EventModels
{
    /// <summary>
    /// An event which reports the deletion of the scale group.
    /// </summary>
    public class ScaleGroupDeleted : TelemetryEvent
    {
        /// <summary>
        /// The name of the scale group.
        /// </summary>
        public string ScaleGroup { get; set; }
    }
}
