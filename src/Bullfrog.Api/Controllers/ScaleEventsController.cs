﻿using System;
using System.Collections.Generic;
using System.Fabric;
using System.Linq;
using System.Threading.Tasks;
using Bullfrog.Actors.Interfaces;
using Bullfrog.Api.Models;
using Eshopworld.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.ServiceFabric.Actors.Client;

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
        /// <param name="bigBrother">The BigBrother instance.</param>
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
        /// <returns></returns>
        [HttpGet("{scaleGroup}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesDefaultResponseType]
        public async Task<ActionResult<IEnumerable<ScheduledScaleEvent>>> ListScheduledEvents(string scaleGroup)
        {
            var regions = await ListRegionsOfScaleGroup(scaleGroup);
            if (regions == null)
            {
                return NotFound();
            }

            var tasks = regions
                .Select(rg => GetActor<IScaleManager>(scaleGroup, rg).ListScaleEvents(default))
                .ToList();
            try
            {
                await Task.WhenAll(tasks);
            }
            catch (Exception ex)
            {
                _bigBrother.Publish(ex);
                throw;
            }

            var events = new Dictionary<Guid, ScheduledScaleEvent>();
            for (int i = 0; i < regions.Count; i++)
            {
                foreach (var scaleEvent in tasks[i].Result)
                {
                    if (!events.TryGetValue(scaleEvent.Id, out var se))
                    {
                        se = new ScheduledScaleEvent
                        {
                            Id = scaleEvent.Id,
                            Name = scaleEvent.Name,
                            EstimatedScaleUpAt = scaleEvent.EstimatedScaleUpAt,
                            RegionConfig = new List<RegionScaleValue>
                            {
                               new RegionScaleValue
                               {
                                   Name = regions[i],
                                   Scale = scaleEvent.Scale,
                               },
                            },
                            RequiredScaleAt = scaleEvent.RequiredScaleAt,
                            StartScaleDownAt = scaleEvent.StartScaleDownAt,
                        };
                        events.Add(scaleEvent.Id, se);
                    }
                    else
                    {
                        se.RegionConfig.Add(new RegionScaleValue
                        {
                            Name = regions[i],
                            Scale = scaleEvent.Scale,
                        });
                        if (scaleEvent.EstimatedScaleUpAt < se.EstimatedScaleUpAt)
                        {
                            se.EstimatedScaleUpAt = scaleEvent.EstimatedScaleUpAt;
                        }
                    }
                }
            }

            return events.Values;
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
            var regions = await ListRegionsOfScaleGroup(scaleGroup);
            if (regions == null)
            {
                return NotFound();
            }

            var tasks = regions
                .Select(rg => GetActor<IScaleManager>(scaleGroup, rg).GetScaleEvent(eventId, default))
                .ToList();
            try
            {
                await Task.WhenAll(tasks);
            }
            catch (Exception ex)
            {
                _bigBrother.Publish(ex);
                throw;
            }

            var anyFound = tasks.FirstOrDefault(t => t.Result != null)?.Result;

            if (anyFound == null)
            {
                return NotFound();
            }

            var scheduledScaleEvent = new ScheduledScaleEvent
            {
                Id = eventId,
                Name = anyFound.Name,
                RequiredScaleAt = anyFound.RequiredScaleAt,
                StartScaleDownAt = anyFound.StartScaleDownAt,
                EstimatedScaleUpAt = tasks
                    .Where(t => t.Result != null)
                    .Min(t => t.Result.EstimatedScaleUpAt),
                RegionConfig = new List<RegionScaleValue>(),
            };

            for (int i = 0; i < regions.Count; i++)
            {
                if (tasks[i].Result != null)
                {
                    scheduledScaleEvent.RegionConfig.Add(new RegionScaleValue
                    {
                        Name = regions[i],
                        Scale = tasks[i].Result.Scale,
                    });
                }
            }

            return scheduledScaleEvent;
        }

        /// <summary>
        /// Creates or updates the scale event.
        /// </summary>
        /// <param name="scaleGroup">The scale group.</param>
        /// <param name="eventId">The scale event ID.</param>
        /// <param name="scaleEvent">The new definition of the scale group.</param>
        /// <returns></returns>
        [HttpPut("{scaleGroup}/{eventId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesDefaultResponseType]
        [Authorize(Policy = AuthenticationPolicies.EventsManagerScope)]
        public async Task<ActionResult<ScaleEvent>> SaveScaleEvent(string scaleGroup, Guid eventId, ScaleEvent scaleEvent)
        {
            var regions = await ListRegionsOfScaleGroup(scaleGroup);
            if (regions == null)
            {
                return NotFound();
            }

            var unknownRegions = scaleEvent.RegionConfig.Select(r => r.Name).Except(regions).ToList();
            if (unknownRegions.Count > 0)
            {
                return NotFound();
            }

            Actors.Interfaces.Models.ScaleEvent CreateScaleEvent(string region)
            {
                return new Actors.Interfaces.Models.ScaleEvent
                {
                    Id = eventId,
                    Name = scaleEvent.Name,
                    RequiredScaleAt = scaleEvent.RequiredScaleAt,
                    StartScaleDownAt = scaleEvent.StartScaleDownAt,
                    Scale = scaleEvent.RegionConfig.Find(r => r.Name == region).Scale,
                };
            }

            var tasks = regions
                .Select(rg => GetActor<IScaleManager>(scaleGroup, rg).ScheduleScaleEvent(CreateScaleEvent(rg), default))
                .ToList();
            try
            {
                await Task.WhenAll(tasks);
            }
            catch (Exception ex)
            {
                // TODO: should we cancel other successfully created scale requests? if not, what should be returned?
                // TODO: what about partially successful updates?
                _bigBrother.Publish(ex);
                throw;
            }

            return scaleEvent;
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
            var regions = await ListRegionsOfScaleGroup(scaleGroup);
            if (regions == null)
            {
                return NotFound();
            }

            var tasks = regions
                .Select(rg => GetActor<IScaleManager>(scaleGroup, rg).DeleteScaleEvent(eventId, default))
                .ToList();
            try
            {
                await Task.WhenAll(tasks);
            }
            catch (Exception ex)
            {
                _bigBrother.Publish(ex);
                throw;
            }

            return tasks.Any(t => t.Result == Actors.Interfaces.Models.ScaleEventState.Executing)
                ? (ActionResult)Accepted()
                : NoContent();
        }
    }
}
