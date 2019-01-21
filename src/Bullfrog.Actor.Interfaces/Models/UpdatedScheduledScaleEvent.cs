using System;
using System.Collections.Generic;
using System.Text;

namespace Bullfrog.Actors.Interfaces.Models
{
    public class UpdatedScheduledScaleEvent : ScheduledScaleEvent
    {
        /// <summary>
        /// The state of the event before the operation has begun
        /// </summary>
        public ScaleEventState PreState { get; set; }
    }
}
