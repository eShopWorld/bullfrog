using System;
using System.Collections.Generic;

namespace Bullfrog.Actors.Models
{
    public class FindNextWakeUpTimeEvent : Eshopworld.Core.TimedTelemetryEvent
    {
        public bool IsRefreshRequired { get; set; }
        public List<ManagedScaleEvent> ScaleEvents { get; set; }
        public DateTimeOffset? NextWakeUpTime { get; set; }
        public TimeSpan ScaleSetPrescaleLeadTime { get; set; }
        public TimeSpan CosmosDbPrescaleLeadTime { get; set; }
    }
}
