using System;

namespace Bullfrog.Actors.Interfaces.Models
{
    /// <summary>
    /// The details of an existing scale event.
    /// </summary>
    public class ScheduledScaleEvent
    {
        /// <summary>
        /// The scale event ID.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// The scale event name.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The time when the scale event starts.
        /// </summary>
        public DateTimeOffset RequiredScaleAt { get; set; }

        /// <summary>
        /// The time when resources are planed to scale out to prepare for the event.
        /// </summary>
        public DateTimeOffset EstimatedScaleUpAt { get; set; }

        /// <summary>
        /// The time when resources can be scaled down.
        /// </summary>
        public DateTimeOffset StartScaleDownAt { get; set; }

        /// <summary>
        /// The requested scale.
        /// </summary>
        public int Scale { get; set; }
    }
}
