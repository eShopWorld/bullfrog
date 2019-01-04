﻿using System;
using System.Collections.Generic;
using System.Fabric;
using System.Linq;
using System.Threading.Tasks;
using Bullfrog.Actor.Interfaces;
using Bullfrog.Api.Models;
using Eshopworld.Core;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;

namespace Bullfrog.Api.Controllers
{
    /// <summary>
    /// Provides details about current state of scale groups.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Policy = AuthenticationPolicies.EventsReaderScope)]
    public class ScaleGroupsController : BaseManagementController
    {
        /// <summary>
        /// Creates an instance of <see cref="ScaleGroupsController"/>.
        /// </summary>
        /// <param name="hostingEnvironment">The hosting environment.</param>
        /// <param name="bigBrother">The BigBrather instance.</param>
        /// <param name="sfContext">The stateless service context.</param>
        public ScaleGroupsController(IHostingEnvironment hostingEnvironment, IBigBrother bigBrother, StatelessServiceContext sfContext)
            : base(sfContext)
        {
        }

        /// <summary>
        /// Gets the current state of the specified scale group.
        /// </summary>
        /// <param name="scaleGroup">The name of the scale group.</param>
        /// <returns></returns>
        [HttpGet("scaleGroup")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesDefaultResponseType]
        public async Task<ActionResult<ScaleGroupState>> GetCurrentState(string scaleGroup)
        {
            var regions = await ListRegionsOfScaleGroup(scaleGroup);
            if (regions == null)
            {
                return NotFound();
            }

            var tasks = regions
                .Select(rg => GetActor<IScaleManager>(scaleGroup, rg).GetScaleSet(default))
                .ToList();
            try
            {
                await Task.WhenAll(tasks);
            }
            catch (Exception)
            {
                throw;
            }

            var scaleRegionStates = new List<ScaleRegionState>();
            for (int i = 0; i < regions.Count; i++)
            {
                if (tasks[i].Result != null)
                {
                    scaleRegionStates.Add(new ScaleRegionState
                    {
                        Name = regions[i],
                        Scale = tasks[i].Result.Scale,
                        WasScaledUpAt = tasks[i].Result.WasScaleUpAt,
                        WillScaleDownAt = tasks[i].Result.WillScaleDownAt,
                    });
                }
            }

            return new ScaleGroupState
            {
                Regions = scaleRegionStates,
            };
        }
    }
}
