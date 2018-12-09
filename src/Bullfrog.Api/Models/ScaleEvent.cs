using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace Bullfrog.Api.Models
{
    public class ScaleEvent
    {
        [Required]
        public string Name { get; set; }

        public DateTimeOffset RequiredScaleAt { get; set; }

        public DateTimeOffset StartScaleDownAt { get; set; }

        public List<RegionScaleValue> RegionConfig { get; set; }
    }
}
