using System;
using System.Collections.Generic;
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
        /// <returns></returns>
        Task ConfigureScaleGroup(string name, ScaleGroupDefinition definition);

        /// <summary>
        /// Returns the current definiton of the scale group (or null if the scale group is not defined).
        /// </summary>
        /// <param name="name">The name of the scale group.</param>
        /// <returns>Returns the current scale group definiton or nul if the scale group is not defined.</returns>
        Task<ScaleGroupDefinition> GetScaleGroupConfiguration(string name);

        /// <summary>
        /// List all defined scale groups.
        /// </summary>
        /// <returns>The list of scale group names.</returns>
        Task<List<string>> ListConfiguredScaleGroup();

        /// <summary>
        /// Saves a scale event.
        /// </summary>
        /// <param name="scaleGroup">The name of the scale group.</param>
        /// <param name="eventId">The id of the event.</param>
        /// <param name="scaleEvent">The scale event details.</param>
        /// <returns>A task.</returns>
        Task<SaveScaleEventReturnValue> SaveScaleEvent(string scaleGroup, Guid eventId, ScaleEvent scaleEvent);

        /// <summary>
        /// Returns an existing scale event.
        /// </summary>
        /// <param name="scaleGroup">The name of the scale group</param>
        /// <param name="eventId">The scale event ID.</param>
        /// <returns></returns>
        Task<ScheduledScaleEvent> GetScaleEvent(string scaleGroup, Guid eventId);

        /// <summary>
        /// List all defined scale events.
        /// </summary>
        /// <param name="scaleGroup">The name of the scale group</param>
        /// <returns>The list of scale events.</returns>
        Task<List<ScheduledScaleEvent>> ListScaleEvents(string scaleGroup);

        /// <summary>
        /// Removes the specified scale event.
        /// </summary>
        /// <param name="scaleGroup">The scale group name.</param>
        /// <param name="eventId">The scale event to remove.</param>
        /// <returns>The state of removed scale event.</returns>
        Task<ScaleEventState> DeleteScaleEvent(string scaleGroup, Guid eventId);

        /// <summary>
        /// Gets the scale manager state.
        /// </summary>
        /// <returns>The state of the scale manager.</returns>
        Task<ScaleGroupState> GetScaleState(string scaleGroup);

        /// <summary>
        /// Scale manager notification about changes to state of scale events.
        /// </summary>
        /// <param name="scaleGroup">The scale group name.</param>
        /// <param name="region">The region name.</param>
        /// <param name="changes">The lists of events with their new states.</param>
        /// <returns>The task</returns>
        Task ReportScaleEventState(string scaleGroup, string region, List<ScaleEventStateChange> changes);
    }
}
