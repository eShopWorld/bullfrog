using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Bullfrog.Actors.Interfaces.Models;
using Microsoft.ServiceFabric.Actors;
using Microsoft.ServiceFabric.Actors.Remoting.FabricTransport;
using Microsoft.ServiceFabric.Services.Remoting;

[assembly: FabricTransportActorRemotingProvider(RemotingListenerVersion = RemotingListenerVersion.V2, RemotingClientVersion = RemotingClientVersion.V2)]
namespace Bullfrog.Actors.Interfaces
{
    /// <summary>
    /// Represents an actor which scales Azure resources for specific scale group and region.
    /// </summary>
    public interface IScaleManager : IActor
    {
        /// <summary>
        /// Creates or updates the scale event.
        /// </summary>
        /// <param name="scaleEvent">The scale event details.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The updated scale event details.</returns>
        Task<UpdatedScheduledScaleEvent> ScheduleScaleEvent(ScaleEvent scaleEvent, CancellationToken cancellationToken);

        /// <summary>
        /// Returns an existing scale event.
        /// </summary>
        /// <param name="id">The scale event ID.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        Task<ScheduledScaleEvent> GetScaleEvent(Guid id, CancellationToken cancellationToken);

        /// <summary>
        /// List all defined scale events.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The list of scale events.</returns>
        Task<List<ScheduledScaleEvent>> ListScaleEvents(CancellationToken cancellationToken);

        /// <summary>
        /// Removes the specified scale event.
        /// </summary>
        /// <param name="id">The scale event to remove.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The state of removed scale event.</returns>
        Task<ScaleEventState> DeleteScaleEvent(Guid id, CancellationToken cancellationToken);

        /// <summary>
        /// Gets the scale manager state.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The state of the scale manager.</returns>
        Task<ScaleState> GetScaleSet(CancellationToken cancellationToken);

        /// <summary>
        /// Disables the scale set manager.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        Task Disable(CancellationToken cancellationToken);

        /// <summary>
        /// Configures the scale manager.
        /// </summary>
        /// <param name="configuration">The new configuration of the scale manager.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        Task Configure(ScaleManagerConfiguration configuration, CancellationToken cancellationToken);
    }
}
