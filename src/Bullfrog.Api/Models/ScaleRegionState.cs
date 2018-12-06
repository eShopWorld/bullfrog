using System;

namespace Bullfrog.Api.Models
{
    public class ScaleRegionState
    {
        public string Name { get; set; }

        public DateTimeOffset WasScaledUpAt { get; set; }

        public DateTimeOffset WillScaleDownAt { get; set; }

        public int Scale { get; set; }
    }
}
