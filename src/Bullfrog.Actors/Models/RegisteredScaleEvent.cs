using System;
using System.Collections.Generic;
using System.Linq;
using Bullfrog.Actors.Interfaces.Models;
using Bullfrog.DomainEvents;

namespace Bullfrog.Actors.Models
{
    /// <summary>
    /// The scale event with states of all regions.
    /// </summary>
    public class RegisteredScaleEvent
    {
        /// <summary>
        /// The user defined name.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The time when the event starts.
        /// </summary>
        public DateTimeOffset RequiredScaleAt { get; set; }

        /// <summary>
        /// The time when the event ends.
        /// </summary>
        public DateTimeOffset StartScaleDownAt { get; set; }

        /// <summary>
        /// The region specific details of the event.
        /// </summary>
        public Dictionary<string, ScaleEventRegionState> Regions { get; set; }

        /// <summary>
        /// The lastest published state of the event.
        /// </summary>
        public ScaleChangeType? ReportedState { get; set; }

        /// <summary>
        /// Creates an <see cref="ScheduledScaleEvent"/> instance based on this instance.
        /// </summary>
        /// <param name="eventId">The event id.</param>
        /// <param name="leadTime">The lead time of the event.</param>
        /// <returns></returns>
        public ScheduledScaleEvent ToScheduledScaleEvent(Guid eventId, TimeSpan leadTime)
        {
            return new ScheduledScaleEvent
            {
                EstimatedScaleUpAt = RequiredScaleAt - leadTime,
                RequiredScaleAt = RequiredScaleAt,
                StartScaleDownAt = StartScaleDownAt,
                Id = eventId,
                Name = Name,
                RegionConfig = Regions.Select(r => new RegionScaleValue
                {
                    Name = r.Key,
                    Scale = r.Value.Scale,
                }).ToList(),
            };
        }

        /// <summary>
        /// The current state of the event 
        /// </summary>
        public ScaleChangeType CurrentState
        {
            get
            {
                if (Regions.Count == 1)
                    return Regions.First().Value.State;

                var states = Regions.Values.Select(r => r.State).ToHashSet();
                if (states.Contains(ScaleChangeType.ScaleInStarted))
                    return ScaleChangeType.ScaleInStarted;
                else if (states.Contains(ScaleChangeType.ScaleInComplete))
                    return states.Count == 1 ? ScaleChangeType.ScaleInComplete : ScaleChangeType.ScaleInStarted;
                else if (states.Contains(ScaleChangeType.ScaleIssue))
                    return ScaleChangeType.ScaleIssue;
                else if (states.Contains(ScaleChangeType.ScaleOutStarted))
                    return ScaleChangeType.ScaleOutStarted;
                else if (states.Contains(ScaleChangeType.ScaleOutComplete))
                    return states.Count == 1 ? ScaleChangeType.ScaleOutComplete : ScaleChangeType.ScaleOutStarted;
                else
                    return ScaleChangeType.Waiting;
            }
        }
    }
}
