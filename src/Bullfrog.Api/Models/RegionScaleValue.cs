namespace Bullfrog.Api.Models
{
    /// <summary>
    /// Defines the scale requirements for the specified region.
    /// </summary>
    public class RegionScaleValue
    {
        /// <summary>
        /// The region name.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The number of requests that the region should be able to handle during the scale event.
        /// </summary>
        public int Scale { get; set; }
    }
}
