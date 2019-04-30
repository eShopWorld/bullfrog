using System;
using System.Collections.Generic;
using System.Linq;
using Bullfrog.Actors.Interfaces.Models;

namespace Bullfrog.Actors.Models
{
    public class RegisteredScaleEvent
    {
        public string Name { get; set; }

        public DateTimeOffset RequiredScaleAt { get; set; }

        public DateTimeOffset StartScaleDownAt { get; set; }

        public Dictionary<string, ScaleEventRegionState> Regions { get; set; }

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
    }
}
