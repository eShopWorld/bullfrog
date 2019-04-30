using System.Collections.Generic;
using System.Fabric;
using System.Linq;
using System.Threading.Tasks;
using Bullfrog.Actors.Interfaces;
using Bullfrog.Actors.Interfaces.Models;
using Eshopworld.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.ServiceFabric.Actors;
using Microsoft.ServiceFabric.Actors.Client;

namespace Bullfrog.Api.Controllers
{
    /// <summary>
    /// Manages configuration.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Policy = AuthenticationPolicies.AdminScope)]
    public class ConfigurationsController : BaseManagementController
    {
        private readonly IBigBrother _bigBrother;

        /// <summary>
        /// Creates an instance of <see cref="ConfigurationsController"/>.
        /// </summary>
        /// <param name="statelessServiceContext">The instance of <see cref="StatelessServiceContext"/>.</param>
        /// <param name="proxyFactory">A factory used to create actor proxies.</param>
        /// <param name="bigBrother">Telemetry client.</param>
        public ConfigurationsController(StatelessServiceContext statelessServiceContext, IActorProxyFactory proxyFactory, IBigBrother bigBrother)
            : base(statelessServiceContext, proxyFactory)
        {
            _bigBrother = bigBrother;
        }

        /// <summary>
        /// Lists names of all configured scale groups.
        /// </summary>
        /// <returns></returns>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesDefaultResponseType]
        [HttpGet]
        public async Task<List<string>> ListScaleGroups()
        {
            return await GetConfigurationManager().ListConfiguredScaleGroup(default);
        }

        /// <summary>
        /// Gets the definition of the specified scale group.
        /// </summary>
        /// <param name="scaleGroup">The scale group name.</param>
        /// <returns></returns>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesDefaultResponseType]
        [HttpGet("{scaleGroup}")]
        public async Task<ActionResult<ScaleGroupDefinition>> GetDefinition(string scaleGroup)
        {
            var configuration = await GetConfigurationManager().GetScaleGroupConfiguration(scaleGroup, default);
            if (configuration != null)
                return configuration;
            else
                return NotFound();
        }

        /// <summary>
        /// Configures the specified scale group.
        /// </summary>
        /// <param name="scaleGroup">The name of the scale group to configure.</param>
        /// <param name="definition">The new or updated configuration of the scale group.</param>
        /// <returns></returns>
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesDefaultResponseType]
        [HttpPut("{scaleGroup}")]
        public async Task<ActionResult> SetDefinition(string scaleGroup, ScaleGroupDefinition definition)
        {
            await GetConfigurationManager().ConfigureScaleGroup(scaleGroup, definition, default);
            _bigBrother.Publish(new Models.EventModels.ConfigurationChanged
            {
                ScaleGroup = scaleGroup,
            });
            return NoContent();
        }

        /// <summary>
        /// Removes the scale group.
        /// </summary>
        /// <param name="scaleGroup">The name of the scale group.</param>
        /// <returns></returns>
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesDefaultResponseType]
        [HttpDelete("{scaleGroup}")]
        public async Task<ActionResult> RemoveDefinition(string scaleGroup)
        {
            await GetConfigurationManager().ConfigureScaleGroup(scaleGroup, null, default);
            _bigBrother.Publish(new Models.EventModels.ScaleGroupDeleted
            {
                ScaleGroup = scaleGroup,
            });
            return NoContent();
        }
    }
}
