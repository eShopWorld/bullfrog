using System;
using System.Collections.Generic;
using System.Fabric;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.ServiceFabric.Actors.Client;
using Bullfrog.Actors.Interfaces.Models;
using Bullfrog.Common;

namespace Bullfrog.Api.Controllers
{
    /// <summary>
    /// Manges scale events.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Policy = AuthenticationPolicies.EventsReaderScope)]
    public class ScaleEventsController : BaseManagementController
    {
        /// <summary>
        /// Creates an instance of the <see cref="ScaleEventsController"/>.
        /// </summary>
        /// <param name="sfContext">The Service Fabric context.</param>
        /// <param name="proxyFactory">The actor proxy factory.</param>
        public ScaleEventsController(
            StatelessServiceContext sfContext,
            IActorProxyFactory proxyFactory)
            : base(sfContext, proxyFactory)
        {
        }

        /// <summary>
        /// Lists all scheduled events from the specified scale group.
        /// </summary>
        /// <param name="scaleGroup">The name of the scale group.</param>
        /// <returns></returns>
        [HttpGet("{scaleGroup}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesDefaultResponseType]
        public async Task<ActionResult<IEnumerable<ScheduledScaleEvent>>> ListScheduledEvents(string scaleGroup)
        {
            try
            {
                return await GetConfigurationManager().ListScaleEvents(scaleGroup);
            }
            catch (AggregateException agEx) when (agEx.InnerException is ScaleGroupNotFoundException)
            {
                return NotFound();
            }
        }

        /// <summary>
        /// Gets the specified scale event.
        /// </summary>
        /// <param name="scaleGroup">The scale group which own the event.</param>
        /// <param name="eventId">The scale event ID.</param>
        /// <returns></returns>
        [HttpGet("{scaleGroup}/{eventId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesDefaultResponseType]
        public async Task<ActionResult<ScheduledScaleEvent>> GetScheduledEvent(string scaleGroup, Guid eventId)
        {
            try
            {
                return await GetConfigurationManager().GetScaleEvent(scaleGroup, eventId);
            }
            catch (AggregateException agEx) when (agEx.InnerException is ScaleGroupNotFoundException)
            {
                return NotFound();
            }
            catch (AggregateException agEx) when (agEx.InnerException is ScaleEventNotFoundException)
            {
                return NotFound();
            }
        }

        /// <summary>
        /// Creates or updates the scale event.
        /// </summary>
        /// <param name="scaleGroup">The scale group.</param>
        /// <param name="eventId">The scale event ID.</param>
        /// <param name="scaleEvent">The new definition of the scale group.</param>
        /// <returns></returns>
        [HttpPut("{scaleGroup}/{eventId}")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status202Accepted)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesDefaultResponseType]
        [Authorize(Policy = AuthenticationPolicies.EventsManagerScope)]
        public async Task<ActionResult<ScheduledScaleEvent>> SaveScaleEvent(string scaleGroup, Guid eventId, ScaleEvent scaleEvent)
        {
            try
            {
                var (result, scheduledScaleEvent) = await GetConfigurationManager().SaveScaleEvent(scaleGroup, eventId, scaleEvent);
                switch (result)
                {
                    case SaveScaleEventResult.Created:
                        return CreatedAtAction(nameof(GetScheduledEvent), new { scaleGroup, eventId }, scheduledScaleEvent);
                    case SaveScaleEventResult.ReplacedExecuting:
                        return Accepted();
                    case SaveScaleEventResult.ReplacedWaiting:
                        return NoContent();
                    default:
                        throw new NotSupportedException($"The {result} value is not supported.");
                }
            }
            catch (AggregateException agEx) when (agEx.InnerException is ScaleEventSaveException ex)
            {
                string code;
                switch (ex.Reason)
                {
                    case ScaleEventSaveFailureReason.RegistrationInThePast:
                        code = "-1";
                        break;
                    case ScaleEventSaveFailureReason.ScaleLimitExceeded:
                        code = "-2";
                        break;
                    case ScaleEventSaveFailureReason.InvalidRegionName:
                        code = "-3";
                        break;
                    default:
                        throw new NotImplementedException($"The value {ex.Reason} is invalid.");
                }
                return BadRequest(new
                {
                    Errors = new[]
                    {
                        new {
                            Code = code,
                            ex.Message,
                        }
                    }
                });
            }
            catch (AggregateException agEx) when (agEx.InnerException is ScaleGroupNotFoundException)
            {
                return NotFound();
            }
        }

        /// <summary>
        /// Deletes the scale event.
        /// </summary>
        /// <param name="scaleGroup">The scale group which owns the event.</param>
        /// <param name="eventId">The ID of the event to delete.</param>
        /// <returns></returns>
        [HttpDelete("{scaleGroup}/{eventId}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status202Accepted)]
        [ProducesDefaultResponseType]
        [Authorize(Policy = AuthenticationPolicies.EventsManagerScope)]
        public async Task<ActionResult> DeleteScaleEvent(string scaleGroup, Guid eventId)
        {
            try
            {
                var state  = await GetConfigurationManager().DeleteScaleEvent(scaleGroup, eventId);
                switch (state)
                {
                    case ScaleEventState.Waiting:
                        return NoContent();
                    case ScaleEventState.Executing:
                        return Accepted();
                    case ScaleEventState.Completed:
                        return NoContent();
                    default:
                        throw new BullfrogException($"The invalid value of scale event state '{state}'");
                }
            }
            catch (AggregateException agEx) when (agEx.InnerException is ScaleGroupNotFoundException)
            {
                return NotFound();
            }
            catch (AggregateException agEx) when (agEx.InnerException is ScaleEventNotFoundException)
            {
                return NotFound();
            }
        }
    }
}
