using System.Collections.Generic;
using System.Linq;
using Bullfrog.DomainEvents;

namespace Bullfrog.Actors.Models
{
    public class ScaleEventCurrentState
    {
        /// <summary>
        /// The last state reported by a region.
        /// </summary>
        public Dictionary<string, ScaleChangeType> Regions { get; set; }

        /// <summary>
        /// The lastest published state of the event.
        /// </summary>
        public ScaleChangeType? ReportedState { get; set; }

        /// <summary>
        /// The current state of the event 
        /// </summary>
        public ScaleChangeType CurrentState
        {
            get
            {
                if (Regions.Count == 1)
                    return Regions.First().Value;

                var states = Regions.Values.ToHashSet();
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
                    return ScaleChangeType.Waiting;
            }
        }
    }
}
