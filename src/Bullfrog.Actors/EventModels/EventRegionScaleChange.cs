using System;
using Bullfrog.DomainEvents;
using Eshopworld.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Bullfrog.Actors.EventModels
{
    public class EventRegionScaleChange : TelemetryEvent
    {
        /// <summary>
        /// The id of the scale event.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// The type of the change.
        /// </summary>
        [JsonConverter(typeof(StringEnumConverter))]
        public ScaleChangeType Type { get; set; }

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
