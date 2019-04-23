using System;
using System.Diagnostics;
using Bullfrog.Actors.Interfaces.Models;

namespace Bullfrog.Actors.Models
{
    [DebuggerDisplay("{Id} {Scale} {RequiredScaleAt} {StartScaleDownAt}")]
    public class ManagedScaleEvent
    {
        public Guid Id { get; set; }

        public string Name { get; set; }

        public DateTimeOffset RequiredScaleAt { get; set; }

        public DateTimeOffset EstimatedScaleUpAt { get; set; }

        public DateTimeOffset StartScaleDownAt { get; set; }

        public int Scale { get; set; }

        public bool IsActive(DateTimeOffset now, TimeSpan leadTime)
            => RequiredScaleAt - leadTime <= now && now < StartScaleDownAt;

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
