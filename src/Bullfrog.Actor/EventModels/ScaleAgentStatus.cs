using System;
using System.Collections.Generic;
using Eshopworld.Core;

namespace Bullfrog.Actors.EventModels
{
    public class ScaleAgentStatus : TelemetryEvent
    {
        public string ActorId { get; set; }

        public int? RequestedScale { get; set; }

        public int ScaleSetInstances { get; set; }

        public List<CosmosScale> Cosmos { get; set; }

        public DateTimeOffset? NextWakeUpTime { get; set; }
    }
}
