using Eshopworld.Core;

namespace Bullfrog.Actors.EventModels
{
    public class CosmosThroughputReport : TelemetryEvent
    {
        public string ScalerName { get; set; }

        public int RequestedThroughput { get; set; }

        public int PreviousRequestUnits { get; set; }

        public int NewRequestUnits { get; set; }
    }
}
