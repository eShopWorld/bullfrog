using System;
using System.Collections.Generic;
using Eshopworld.Core;

namespace Bullfrog.Actors.EventModels
{
    public class ScaleAgentStatus : TelemetryEvent
    {
        public string ActorId { get; set; }

        public int? RequestedScaleSetScale { get; set; }

        public int? RequestedCosmosDbScale { get; set; }

        public List<ScaleSetScale> ScaleSets { get; set; }

        public List<CosmosScale> Cosmos { get; set; }

        public DateTimeOffset? NextWakeUpTime { get; set; }
    }
}
