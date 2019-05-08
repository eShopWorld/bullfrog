using System;
using Bullfrog.DomainEvents;
using Eshopworld.Core;

namespace Bullfrog.Actors.EventModels
{
    public class EventRegionScaleChange : TelemetryEvent
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
        /// The name of the region.
        /// </summary>
        public string RegionName { get; set; }

        /// <summary>
        /// The name of the scale group
        /// </summary>
        public string ScaleGroup { get; set; }
    }
}
