using System.Diagnostics.CodeAnalysis;
using Eshopworld.Core;

namespace Bullfrog.Actors.EventModels
{
    [ExcludeFromCodeCoverage]
    public class ScaleSetModified : TelemetryEvent
    {
        public string ScalerName { get; set; }

        public int RequestedThroughput { get; set; }

        public int RequestedInstances { get; set; }

        public int ConfiguredInstances { get; set; }
    }
}
