using System.Diagnostics.CodeAnalysis;
using Eshopworld.Core;

namespace Bullfrog.Actors.EventModels
{
    [ExcludeFromCodeCoverage]
    public class ScaleSetThroughput : TelemetryEvent
    {
        public string ScalerName { get; set; }

        public int RequestedThroughput { get; set; }

        public int RequiredInstances { get; set; }

        public int ConfiguredInstances { get; set; }

        public int WorkingInstances { get; set; }

        public int AvailableThroughput { get; set; }
    }
}
