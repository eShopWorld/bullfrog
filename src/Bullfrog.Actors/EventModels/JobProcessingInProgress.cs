using System;

namespace Bullfrog.Actors.EventModels
{
    public class JobProcessingInProgress : JobTelemetryEvent
    {
        public DateTimeOffset JobStartTime { get; set; }

        public DateTimeOffset JobExpectedCompletionTime { get; set; }
    }
}
