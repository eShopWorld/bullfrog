using System;
using System.Collections.Generic;
using System.Text;

namespace Bullfrog.Actor.Interfaces.Models
{
    public class ScaleState
    {
        public DateTimeOffset WasScaleUpAt { get; set; }

        public DateTimeOffset WillScaleDownAt { get; set; }

        public int Scale { get; set; }
    }
}
