using System;
using System.Collections.Generic;

namespace Bullfrog.Api.Models
{
    /// <summary>
    /// Describes the current state of the region of a scale group.
    /// </summary>
    public class ScaleRegionState
    {
        /// <summary>
        /// The name of the region.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The time when the resources ware scaled out.
        /// </summary>
        public DateTimeOffset WasScaledUpAt { get; set; }

        /// <summary>
        /// The time when the resources will be scaled in to the minimal value.
        /// </summary>
        public DateTimeOffset WillScaleDownAt { get; set; }

        /// <summary>
        /// The number of requests which can be processed currently.
        /// </summary>
        public decimal Scale { get; set; }

        /// <summary>
        /// The scale which is requested by scale events.
        /// </summary>
        public int? RequestedScale { get; set; }

        /// <summary>
        /// State of each scale set.
        /// </summary>
        public IDictionary<string, decimal> ScaleSetState { get; set; }
    }
}
