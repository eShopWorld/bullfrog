using System;
using System.Collections.Generic;
using System.Text;

namespace Bullfrog.Api.Models
{
    /// <summary>
    /// Describes an existing scale event.
    /// </summary>
    public class ScheduledScaleEvent
    {
        /// <summary>
        /// Identifier of the scale event.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Name of the scale event.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The time when all resources should be scaled to the level which allows to handle the requested traffic.
        /// </summary>
        public DateTimeOffset RequiredScaleAt { get; set; }

        /// <summary>
        /// The estimated time when the scaling of resources start.
        /// </summary>
        public DateTimeOffset EstimatedScaleUpAt { get; set; }

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
