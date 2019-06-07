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

        // TODO: change the type of the Type property to ScaleChangeType as soon as names (instead of numeric values) are logged in AppInsights
        /// <summary>
        /// The type of the change.
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// Additional information about current change.
        /// </summary>
        public string Details { get; set; }

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
