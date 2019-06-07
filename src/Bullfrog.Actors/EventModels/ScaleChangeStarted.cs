using Eshopworld.Core;

namespace Bullfrog.Actors.EventModels
{
    public class ScaleChangeStarted : TelemetryEvent
    {
        public string ResourceType { get; set; }

        public int RequestedThroughput { get; set; }

        public bool PreviousChangeNotCompleted { get; set; }
    }
}
