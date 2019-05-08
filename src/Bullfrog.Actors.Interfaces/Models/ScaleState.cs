using System;
using System.Collections.Generic;

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
        public decimal Scale { get; set; }

        /// <summary>
        /// The scale which is requested by scale events.
        /// </summary>
        public int? RequestedScale { get; set; }

        /// <summary>
        /// State of each scale set.
        /// </summary>
        public Dictionary<string, decimal> ScaleSetState { get; set; }
    }
}
