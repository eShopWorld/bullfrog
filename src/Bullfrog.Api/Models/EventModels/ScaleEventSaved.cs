using System;
using System.Collections.Generic;
using Bullfrog.Actors.Interfaces.Models;
using Eshopworld.Core;

namespace Bullfrog.Api.Models.EventModels
{
    /// <summary>
    /// The event that reports a successful scale event has been saved
    /// </summary>
    public class ScaleEventSaved : TelemetryEvent
    {
        /// <summary>
        /// The name of the updated scale group.
        /// </summary>
        public string ScaleGroup { get; set; }

        /// <summary>
        /// The name of the updated event id.
        /// </summary>
        public Guid EventId { get; set; }

        /// <summary>
        /// The scale event name
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The list of regions which require scaling.
        /// </summary>
        public string RegionConfig { get; set; }

        /// <summary>
        /// The time when all resources should be scaled out
        /// </summary>
        public DateTimeOffset RequiredScaleAt { get; set; }

        /// <summary>
        /// The time when the scale event ends and all resources can be scaled in
        /// </summary>
        public DateTimeOffset StartScaleDownAt { get; set; }

    }
}
