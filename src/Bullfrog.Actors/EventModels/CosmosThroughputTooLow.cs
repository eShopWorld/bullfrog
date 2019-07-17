using Eshopworld.Core;

namespace Bullfrog.Actors.EventModels
{
    public class CosmosThroughputTooLow : TelemetryEvent
    {
        public string ScalerName { get; set; }

        public int ThroughputRequired { get; set; }

        public int MinThroughput { get; set; }

        public string ErrorMessage { get; set; }
    }
}
