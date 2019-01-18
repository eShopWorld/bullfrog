using System;
using Bullfrog.Actor.Interfaces.Models;

namespace Bullfrog.Actor.Models
{
    public class ManagedScaleEvent
    {
        public Guid Id { get; set; }

        public string Name { get; set; }

        public DateTimeOffset RequiredScaleAt { get; set; }

        public DateTimeOffset EstimatedScaleUpAt { get; set; }

        public DateTimeOffset StartScaleDownAt { get; set; }

        public int Scale { get; set; }

        public ScaleEventState GetState(DateTimeOffset now)
        {
            if (now < RequiredScaleAt)
                return ScaleEventState.Waiting;
            else if (now <= StartScaleDownAt)
                return ScaleEventState.Executing;
            else
                return ScaleEventState.Completed;
        }
    }
}
