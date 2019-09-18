using System;
using Eshopworld.Core;

namespace Bullfrog.Actors.EventModels
{
    public class PurgingScaleEvent : TelemetryEvent
    {
        public Guid ScaleEventId { get; set; }

        public string Name { get; set; }

        public string RegionsSummary { get; set; }

        public DateTimeOffset RequiredScaleAt { get; set; }

        public DateTimeOffset StartScaleDownAt { get; set; }
    }
}
