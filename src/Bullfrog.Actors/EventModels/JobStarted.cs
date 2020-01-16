using System;

namespace Bullfrog.Actors.EventModels
{
    public class JobStarted : JobTelemetryEvent
    {
        public int InstancesRequested { get; set; }

        public DateTimeOffset? Ends { get; set; }
    }
}
