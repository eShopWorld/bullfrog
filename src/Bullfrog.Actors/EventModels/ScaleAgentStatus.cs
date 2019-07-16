using System;
using Eshopworld.Core;

namespace Bullfrog.Actors.EventModels
{
    public class ScaleAgentStatus : TelemetryEvent
    {
        public int RequestedScaleSetScale { get; set; }

        public int RequestedCosmosDbScale { get; set; }

        public int ScaleRequests { get; internal set; }

        public int ScaleCompleted { get; set; }

        public int ScaleFailing { get; set; }

        public int ScaleLimited { get; set; }

        public int ScaleInProgress { get; set; }

        public DateTimeOffset? NextWakeUpTime { get; set; }
    }
}
