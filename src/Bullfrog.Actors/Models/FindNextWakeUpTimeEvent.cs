using System;
using System.Collections.Generic;

namespace Bullfrog.Actors.Models
{
    public class FindNextWakeUpTimeEvent : Eshopworld.Core.TelemetryEvent
    {
        public bool IsRefreshRequired { get; set; }
        public string ScaleEvents { get; set; }
        public DateTimeOffset? NextWakeUpTime { get; set; }
        public TimeSpan ScaleSetPrescaleLeadTime { get; set; }
        public TimeSpan CosmosDbPrescaleLeadTime { get; set; }
    }
}
