using Eshopworld.Core;

namespace Bullfrog.Actors.EventModels
{
    public class CosmosThroughputTooLow : TelemetryEvent
    {
        public string CosmosAccunt { get; set; }

        public string Database { get; set; }

        public string Container { get; set; }

        public int ThroughputRequired { get; set; }

        public int MinThroughput { get; set; }

        public string ErrorMessage { get; set; }
    }
}
