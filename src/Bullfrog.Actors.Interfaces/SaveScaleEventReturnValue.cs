using Bullfrog.Actors.Interfaces.Models;

namespace Bullfrog.Actors.Interfaces
{
    /// <summary>
    /// The result of saving a scale event.
    /// </summary>
    public class SaveScaleEventReturnValue
    {
        /// <summary>
        /// The way the operation completed.
        /// </summary>
        public SaveScaleEventResult Result { get; set; }

        /// <summary>
        /// The saved scale event.
        /// </summary>
        public ScheduledScaleEvent ScheduledScaleEvent { get; set; }
    }
}
