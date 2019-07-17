using Eshopworld.Core;

namespace Bullfrog.Actors.EventModels
{
    public class ScaleSetReset : TelemetryEvent
    {
        public string ScalerName { get; set; }

        public int ConfiguredInstances { get; set; }
    }
}
