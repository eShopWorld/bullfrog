using System.Collections.Generic;
using System.Threading.Tasks;
using Bullfrog.Actors.Interfaces.Models;
using Microsoft.ServiceFabric.Actors;

namespace Bullfrog.Actors.Interfaces
{
    /// <summary>
    /// Sends domain events about changes in state of scale events.
    /// </summary>
    public interface IScaleEventStateReporter : IActor
    {
        /// <summary>
        /// Sets the list of regions defined in the scale group.
        /// </summary>
        /// <param name="regions"></param>
        /// <returns></returns>
        Task ConfigureRegions(string[] regions);

        /// <summary>
        /// Scale manager notification about changes to state of scale events.
        /// </summary>
        /// <param name="region">The region name.</param>
        /// <param name="changes">The lists of events with their new states.</param>
        /// <returns>The task</returns>
        Task ReportScaleEventState(string region, List<ScaleEventStateChange> changes);
    }
}
