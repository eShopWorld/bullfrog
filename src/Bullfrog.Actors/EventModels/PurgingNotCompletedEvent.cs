using System;
using Bullfrog.DomainEvents;
using Eshopworld.Core;

namespace Bullfrog.Actors.EventModels
{
    public class PurgingNotCompletedEvent : TelemetryEvent
    {
        public Guid ScaleEventId { get; set; }

        public ScaleChangeType State { get; set; }

        public string RegionsSummary { get; set; }
    }
}
