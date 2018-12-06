using System;
using System.Collections.Generic;
using System.Text;

namespace Bullfrog.Actor.Interfaces.Models
{
    public class ScaleEvent
    {
        public Guid Id { get; set; }

        public string Name { get; set; }

        public DateTimeOffset RequiredScaleAt { get; set; }

        public DateTimeOffset StartScaleDownAt { get; set; }

        public int Scale { get; set; }
    }
}
