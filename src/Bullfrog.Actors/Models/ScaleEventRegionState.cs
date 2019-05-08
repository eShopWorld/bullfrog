using Bullfrog.DomainEvents;

namespace Bullfrog.Actors.Models
{
    /// <summary>
    /// Details about a region of a scale event.
    /// </summary>
    [System.Diagnostics.DebuggerDisplay("{State} {Scale}")]
    public class ScaleEventRegionState
    {
        /// <summary>
        /// The scale requested for the region.
        /// </summary>
        public int Scale { get; set; }

        /// <summary>
        /// The state reported by the region.
        /// </summary>
        public ScaleChangeType State { get; set; }
    }
}
