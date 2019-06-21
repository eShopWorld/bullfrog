using Eshopworld.Core;

namespace Bullfrog.Actors.EventModels
{
    public class ResourceScalingIssue : TelemetryEvent
    {
        public string ResourceName { get; set; }

        public string Message { get; set; }
    }
}
