using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Bullfrog.Actor.Interfaces.Models
{
    public class ScaleGroupRegion
    {
        [Required]
        public string RegionName { get; set; }

        [Required]
        public ScaleSetConfiguration ScaleSet { get; set; }
    }
}
