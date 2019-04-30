using System;
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
            try
            {
                return base.Ok(await GetConfigurationManager().GetScaleState(scaleGroup));

            }
            catch (AggregateException agEx) when (agEx.InnerException is ScaleGroupNotFoundException)
            {

                return NotFound();
            }
        }
    }
}
