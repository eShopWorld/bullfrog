﻿using System;
using System.Collections.Generic;
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
        /// <returns>The task.</returns>
        Task ScheduleScaleEvent(RegionScaleEvent scaleEvent);

        /// <summary>
        /// Returns the list of events.
        /// </summary>
        /// <returns></returns>
        Task<List<RegionScaleEvent>> ListEvents();

        /// <summary>
        /// Removes the specified scale event.
        /// </summary>
        /// <param name="id">The scale event to remove.</param>
        /// <returns>The task.</returns>
        Task DeleteScaleEvent(Guid id);

        /// <summary>
        /// Removes specified scale events.
        /// </summary>
        /// <param name="events">Scale events to remove.</param>
        /// <returns></returns>
        Task PurgeScaleEvents(List<Guid> events);

        /// <summary>
        /// Gets the scale manager state.
        /// </summary>
        /// <returns>The state of the scale manager.</returns>
        Task<ScaleState> GetScaleSet();

        /// <summary>
        /// Disables the scale set manager.
        /// </summary>
        /// <returns></returns>
        Task Disable();

        /// <summary>
        /// Configures the scale manager.
        /// </summary>
        /// <param name="configuration">The new configuration of the scale manager.</param>
        /// <returns></returns>
        Task Configure(ScaleManagerConfiguration configuration);
    }
}
