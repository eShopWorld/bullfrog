using System.Diagnostics.CodeAnalysis;
using Eshopworld.Core;

namespace Bullfrog.Actors.EventModels
{
    [ExcludeFromCodeCoverage]
    public class ResourceScalingIssue : TelemetryEvent
    {
        public string ResourceName { get; set; }

        public string Message { get; set; }
    }
}
