using System;
using System.Collections.Generic;
using System.Linq;
using Bullfrog.Actors.Interfaces.Models;
using Bullfrog.DomainEvents;

namespace Bullfrog.Actors.Models
{
    public class RegisteredScaleEvent
    {
        public string Name { get; set; }

        public DateTimeOffset RequiredScaleAt { get; set; }

        public DateTimeOffset StartScaleDownAt { get; set; }

        public Dictionary<string, ScaleEventRegionState> Regions { get; set; }

        public ScaleChangeType? ReportedState { get; set; }

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

        public ScaleChangeType? CurrentState
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
                    return null;
            }
        }
    }
}
