using System;

namespace Bullfrog.Actors.Interfaces.Models
{
    /// <summary>
    /// The scale manager state.
    /// </summary>
    public class ScaleState
    {
        /// <summary>
        /// The beginning of the scale out phase.
        /// </summary>
        public DateTimeOffset WasScaleUpAt { get; set; }

        /// <summary>
        /// The planned end of the scale out phase.
        /// </summary>
        public DateTimeOffset WillScaleDownAt { get; set; }

        /// <summary>
        /// The current scale.
        /// </summary>
        public int Scale { get; set; }
    }
}
