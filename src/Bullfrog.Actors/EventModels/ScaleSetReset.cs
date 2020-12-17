using System.Diagnostics.CodeAnalysis;
using Eshopworld.Core;

namespace Bullfrog.Actors.EventModels
{
    [ExcludeFromCodeCoverage]
    public class ScaleSetReset : TelemetryEvent
    {
        public string ScalerName { get; set; }

        public int ConfiguredInstances { get; set; }
    }
}
