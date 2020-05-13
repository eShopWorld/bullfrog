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
using System.Linq;
using Eshopworld.Core;
using Newtonsoft.Json;

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
        private readonly IBigBrother _bigBrother;

        /// <summary>
        /// Creates an instance of the <see cref="ScaleEventsController"/>.
        /// </summary>
        /// <param name="sfContext">The Service Fabric context.</param>
        /// <param name="proxyFactory">The actor proxy factory.</param>
        /// <param name="bigBrother">Telemetry client.</param>
        public ScaleEventsController(
            StatelessServiceContext sfContext,
            IActorProxyFactory proxyFactory,
            IBigBrother bigBrother)
            : base(sfContext, proxyFactory)
        {
            _bigBrother = bigBrother;
        }

        /// <summary>
        /// Lists all scheduled events from the specified scale group.
        /// </summary>
        /// <param name="scaleGroup">The name of the scale group.</param>
        /// <param name="activeOnly">If true only not completed events are returned.</param>
        /// <param name="fromRegion">Returns events from the specified region.</param>
        /// <returns></returns>
        [HttpGet("{scaleGroup}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesDefaultResponseType]
        public async Task<ActionResult<IEnumerable<ScheduledScaleEvent>>> ListScheduledEvents(string scaleGroup, bool activeOnly = false, string fromRegion = null)
        {
            try
            {
                List<ScheduledScaleEvent> events;
                if (activeOnly || fromRegion != null)
                {
                    events = await GetConfigurationManager().ListScheduledScaleEvents(scaleGroup, new ListScaleEventsParameters
                    {
                        ActiveOnly = activeOnly,
                        FromRegion = fromRegion,
                    });
                }
                else
                {
                    events = await GetConfigurationManager().ListScaleEvents(scaleGroup);
                }

                return Ok(events
                    .OrderBy(ev => ev.RequiredScaleAt)
                    .ThenBy(ev => ev.StartScaleDownAt));

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
                var response = await GetConfigurationManager().SaveScaleEvent(scaleGroup, eventId, scaleEvent);

                _bigBrother.Publish(new Models.EventModels.ScaleEventSaved
                {
                    ScaleGroup = scaleGroup,
                    EventId = eventId,
                    Name = scaleEvent.Name,
                    RegionConfig = JsonConvert.SerializeObject(scaleEvent.RegionConfig),
                    RequiredScaleAt = scaleEvent.RequiredScaleAt,
                    StartScaleDownAt = scaleEvent.StartScaleDownAt
                });

                return response.Result switch
                {
                    SaveScaleEventResult.Created => CreatedAtAction(nameof(GetScheduledEvent), new { scaleGroup, eventId }, response.ScheduledScaleEvent),
                    SaveScaleEventResult.ReplacedExecuting => Accepted(),
                    SaveScaleEventResult.ReplacedWaiting => NoContent(),
                    _ => throw new NotSupportedException($"The {response} value is not supported.")
                };
            }
            catch (AggregateException agEx) when (agEx.InnerException is ScaleEventSaveException ex)
            {
                return BadRequest(new
                {
                    Errors = new[]
                    {
                        new {
                            Code = ((int)ex.Reason).ToString(),
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
                var state = await GetConfigurationManager().DeleteScaleEvent(scaleGroup, eventId);

                return state switch
                {
                    ScaleEventState.Waiting => NoContent(),
                    ScaleEventState.Executing => Accepted(),
                    ScaleEventState.Completed => NoContent(),
                    _ => throw new BullfrogException($"The invalid value of scale event state '{state}'")
                };
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
