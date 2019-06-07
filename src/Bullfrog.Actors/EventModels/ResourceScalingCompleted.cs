using System;
using Eshopworld.Core;

namespace Bullfrog.Actors.EventModels
{
    public class ResourceScalingCompleted : TelemetryEvent
    {
        public string ResourceName { get; set; }

        public int? RequiredThroughput { get; set; }

        public int FinalThroughput { get; set; }

        public bool ThroughputIsLimited => FinalThroughput < (RequiredThroughput ?? 0);

        public TimeSpan Duration { get; set; }
    }
}
