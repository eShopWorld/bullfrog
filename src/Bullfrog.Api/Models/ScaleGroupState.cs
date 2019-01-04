using System.Collections.Generic;

namespace Bullfrog.Api.Models
{
    /// <summary>
    /// Describes the current state of the scale group.
    /// </summary>
    public class ScaleGroupState
    {
        /// <summary>
        /// The list of scale group's regions which currently are scaled out.
        /// </summary>
        public List<ScaleRegionState> Regions { get; set; }
    }
}
