namespace Bullfrog.Actors.Interfaces.Models
{
    /// <summary>
    /// The details of the update of the scale event.
    /// </summary>
    public class UpdatedScheduledScaleEvent : RegionScheduledScaleEvent
    {
        /// <summary>
        /// The state of the event before the operation has begun
        /// </summary>
        public ScaleEventState PreState { get; set; }
    }
}
