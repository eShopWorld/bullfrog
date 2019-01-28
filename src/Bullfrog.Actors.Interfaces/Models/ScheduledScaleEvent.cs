using System;
using System.Collections.Generic;
using System.Text;

namespace Bullfrog.Actors.Interfaces.Models
{
    public class ScheduledScaleEvent
    {
        public Guid Id { get; set; }

        public string Name { get; set; }

        public DateTimeOffset RequiredScaleAt { get; set; }

        public DateTimeOffset EstimatedScaleUpAt { get; set; }

        public DateTimeOffset StartScaleDownAt { get; set; }

        public int Scale { get; set; }
    }
}
