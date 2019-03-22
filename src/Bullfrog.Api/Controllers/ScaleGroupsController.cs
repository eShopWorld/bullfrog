using System;
using System.Collections.Generic;
using System.Fabric;
using System.Linq;
using System.Threading.Tasks;
using Bullfrog.Actors.Interfaces;
using Bullfrog.Api.Models;
using Eshopworld.Core;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.ServiceFabric.Actors.Client;

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
        /// <param name="sfContext">The stateless service context.</param>
        /// <param name="proxyFactory">A factory used to create actor proxies.</param>
        public ScaleGroupsController(
            StatelessServiceContext sfContext,
            IActorProxyFactory proxyFactory)
            : base(sfContext, proxyFactory)
        {
        }

        /// <summary>
        /// Gets the current state of the specified scale group.
        /// </summary>
        /// <param name="scaleGroup">The name of the scale group.</param>
        /// <returns></returns>
        [HttpGet("{scaleGroup}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
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
                        RequestedScale = tasks[i].Result.RequestedScale,
                        WasScaledUpAt = tasks[i].Result.WasScaleUpAt,
                        WillScaleDownAt = tasks[i].Result.WillScaleDownAt,
                        ScaleSetState = tasks[i].Result.ScaleSetState,
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
