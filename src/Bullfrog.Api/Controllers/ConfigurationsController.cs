using System.Collections.Generic;
using System.Fabric;
using System.Threading.Tasks;
using Bullfrog.Actors.Interfaces;
using Bullfrog.Actors.Interfaces.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.ServiceFabric.Actors;

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
        /// <summary>
        /// Creates an instance of <see cref="ConfigurationsController"/>.
        /// </summary>
        /// <param name="statelessServiceContext">The instance of <see cref="StatelessServiceContext"/>.</param>
        public ConfigurationsController(StatelessServiceContext statelessServiceContext)
            : base(statelessServiceContext)
        {
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
        [ProducesResponseType(StatusCodes.Status404NotFound)]
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
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesDefaultResponseType]
        [HttpPut("{scaleGroup}")]
        public async Task<ActionResult> SetDefinition(string scaleGroup, ScaleGroupDefinition definition)
        {
            await GetConfigurationManager().ConfigureScaleGroup(scaleGroup, definition, default);
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
            return NoContent();
        }

        private IConfigurationManager GetConfigurationManager()
        {
            return GetActor<IConfigurationManager>(new ActorId("configuration"));
        }
    }
}
