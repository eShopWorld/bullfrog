using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Bullfrog.Actors.Interfaces.Models;
using Microsoft.ServiceFabric.Actors;

namespace Bullfrog.Actors.Interfaces
{
    /// <summary>
    /// Represents an actor which manages scale groups
    /// </summary>
    public interface IConfigurationManager : IActor
    {
        /// <summary>
        /// Configures a scale group.
        /// </summary>
        /// <param name="name">The name of the scale group.</param>
        /// <param name="definition">The new definiton of the scale group or null if the scale group should be processed.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        Task ConfigureScaleGroup(string name, ScaleGroupDefinition definition, CancellationToken cancellationToken);

        /// <summary>
        /// Returns the current definiton of the scale group (or null if the scale group is not defined).
        /// </summary>
        /// <param name="name">The name of the scale group.</param>
        /// <param name="cancellationToken">The cancelation token.</param>
        /// <returns>Returns the current scale group definiton or nul if the scale group is not defined.</returns>
        Task<ScaleGroupDefinition> GetScaleGroupConfiguration(string name, CancellationToken cancellationToken);

        /// <summary>
        /// List all defined scale groups.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The list of scale group names.</returns>
        Task<List<string>> ListConfiguredScaleGroup(CancellationToken cancellationToken);
    }
}
