using System;
using System.ComponentModel.DataAnnotations;
using Bullfrog.Actors.Interfaces.Models.Validation;

namespace Bullfrog.Actors.Interfaces.Models
{
    public class ScaleEvent
    {
        public Guid Id { get; set; }

        public string Name { get; set; }

        public DateTimeOffset RequiredScaleAt { get; set; }

        [ValueIs(ValueComparision.GreaterThen, PropertyValue = nameof(RequiredScaleAt))]
        public DateTimeOffset StartScaleDownAt { get; set; }

        [Range(1, 1_000_000)]
        public int Scale { get; set; }
    }
}
