using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace Bullfrog.Api.Models
{
    /// <summary>
    /// Defines a new scale event.
    /// </summary>
    public class ScaleEvent
    {
        /// <summary>
        /// The scale event name.
        /// </summary>
        [Required]
        public string Name { get; set; }

        /// <summary>
        /// The time when all resources should be scaled to the level which allows to handle the requested traffic.
        /// </summary>
        public DateTimeOffset RequiredScaleAt { get; set; }

        /// <summary>
        /// The time when the scale event ends and all resources can be scaled in.
        /// </summary>
        public DateTimeOffset StartScaleDownAt { get; set; }

        /// <summary>
        /// The list of regions which require scaling.
        /// </summary>
        public List<RegionScaleValue> RegionConfig { get; set; }
    }
}
