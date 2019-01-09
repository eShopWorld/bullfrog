using System;

namespace Bullfrog.Actor.EventModels
{
    public class ScaleAgentStatus
    {
        public string ActorId { get; set; }

        public int Scale { get; set; }

        public DateTimeOffset? NextWakeUpTime { get; set; }
    }
}
