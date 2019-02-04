using System;
using System.ComponentModel.DataAnnotations;
using Bullfrog.Actors.Interfaces.Models.Validation;

namespace Bullfrog.Actors.Interfaces.Models
{
    /// <summary>
    /// A scale event.
    /// </summary>
    public class ScaleEvent
    {
        /// <summary>
        /// The scale event ID.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// The name of the scale event.
        /// </summary>
        /// <remarks>
        /// Preserved but not used internally.
        /// </remarks>
        public string Name { get; set; }

        /// <summary>
        /// The beginning of the scale event.
        /// </summary>
        public DateTimeOffset RequiredScaleAt { get; set; }

        /// <summary>
        /// The end of the scale event.
        /// </summary>
        [ValueIs(ValueComparison.GreaterThen, PropertyValue = nameof(RequiredScaleAt))]
        public DateTimeOffset StartScaleDownAt { get; set; }

        /// <summary>
        /// The requested scale.
        /// </summary>
        [Range(1, 1_000_000)]
        public int Scale { get; set; }
    }
}
