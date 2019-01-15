using System;

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
        public int Scale { get; set; }
    }
}
