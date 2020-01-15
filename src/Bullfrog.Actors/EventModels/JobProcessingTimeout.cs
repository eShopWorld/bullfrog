using System;

namespace Bullfrog.Actors.EventModels
{

    public class JobProcessingTimeout : JobTelemetryEvent
    {
        public DateTimeOffset JobStartTime { get; set; }

        public DateTimeOffset JobExpectedCompletionTime { get; set; }
    }
}
