using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Fabric;
using System.Linq;
using System.Threading.Tasks;
using Bullfrog.Actor.Interfaces;
using Bullfrog.Api.Models;
using Eshopworld.Core;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.ServiceFabric.Actors;
using Microsoft.ServiceFabric.Actors.Client;

namespace Bullfrog.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ScaleEventsController : BaseManagementController
    {
        public ScaleEventsController(IHostingEnvironment hostingEnvironment, IBigBrother bigBrother, StatelessServiceContext sfContext)
            : base(sfContext)
        {
        }

        [HttpGet("{scaleGroup}")]
        public async Task<IEnumerable<ScheduledScaleEvent>> ListScheduledEvents(string scaleGroup)
        {
            var regions = ListRegionsOfScaleGroup(scaleGroup);

            var tasks = regions
                .Select(rg => GetActor<IScaleManager>(scaleGroup, rg).ListScaleEvents(default))
                .ToList();
            try
            {
                await Task.WhenAll(tasks);
            }
            catch (Exception)
            {
                throw;
            }

            var events = new Dictionary<Guid, ScheduledScaleEvent>();
            for (int i = 0; i < regions.Length; i++)
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

        [HttpGet("{scaleGroup}/{eventId}")]
        public async Task<ActionResult<ScheduledScaleEvent>> ListScheduledEvents(string scaleGroup, Guid eventId)
        {
            var regions = ListRegionsOfScaleGroup(scaleGroup);

            var tasks = regions
                .Select(rg => GetActor<IScaleManager>(scaleGroup, rg).GetScaleEvent(eventId, default))
                .ToList();
            try
            {
                await Task.WhenAll(tasks);
            }
            catch (Exception)
            {
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

            for (int i = 0; i < regions.Count(); i++)
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

        [HttpPut("{scaleGroup}/{eventId}")]
        public async Task<ActionResult> SaveScaleEvent(string scaleGroup, Guid eventId, [Required]ScaleEvent scaleEvent)
        {
            // TODO: move all these validation rules to the model
            if (scaleEvent.RequiredScaleAt <= DateTimeOffset.UtcNow)
            {
                return BadRequest();
            }

            if (scaleEvent.RequiredScaleAt >= scaleEvent.StartScaleDownAt)
            {
                return BadRequest();
            }

            var regions = ListRegionsOfScaleGroup(scaleGroup);
            var unknownRegions = scaleEvent.RegionConfig.Select(r => r.Name).Except(regions).ToList();
            if (unknownRegions.Count > 0)
            {
                return BadRequest();
            }

            Actor.Interfaces.Models.ScaleEvent CreateScaleEvent(string region)
            {
                return new Actor.Interfaces.Models.ScaleEvent
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
            catch (Exception)
            {
                // TODO: should we cancel other successfully created scale requests? if not, what should be returned?
                // TODO: what about partially successful updates?
                throw;
            }

            // TODO: what to do with completed events?


            throw new NotImplementedException();
        }

        [HttpDelete("{scaleGroup}/{eventId}")]
        public async Task<ActionResult> DeleteScaleEvent(string scaleGroup, Guid eventId)
        {
            var regions = ListRegionsOfScaleGroup(scaleGroup);

            var tasks = regions
                .Select(rg => GetActor<IScaleManager>(scaleGroup, rg).DeleteScaleEvent(eventId, default))
                .ToList();
            try
            {
                await Task.WhenAll(tasks);
            }
            catch (Exception)
            {
                throw;
            }

            return tasks.Any(t => t.Result == Actor.Interfaces.Models.ScaleEventState.Executing)
                ? (ActionResult)Accepted()
                : NoContent();
        }
    }
}
