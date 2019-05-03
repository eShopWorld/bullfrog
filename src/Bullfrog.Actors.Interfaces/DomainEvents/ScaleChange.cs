using System;
using Eshopworld.Core;

namespace Bullfrog.DomainEvents
{
    /// <summary>
    /// The domain event reporting the changes to the state of a scale event.
    /// </summary>
    public class ScaleChange : DomainEvent
    {
        /// <summary>
        /// The id of the scale event.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// The type of the schange.
        /// </summary>
        public ScaleChangeType Type { get; set; }

        /// <summary>
        /// The name of the scale group
        /// </summary>
        public string ScaleGroup { get; set; }
    }
}
