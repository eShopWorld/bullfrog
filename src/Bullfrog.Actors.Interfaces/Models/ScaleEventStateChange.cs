using System;
using Bullfrog.DomainEvents;

namespace Bullfrog.Actors.Interfaces.Models
{
    /// <summary>
    /// A notification about change of the state of given scale event.
    /// </summary>
    public class ScaleEventStateChange
    {
        /// <summary>
        /// The scale event id.
        /// </summary>
        public Guid EventId { get; set; }

        /// <summary>
        /// The new state of the scale event.
        /// </summary>
        public ScaleChangeType State { get; set; }
    }
}
