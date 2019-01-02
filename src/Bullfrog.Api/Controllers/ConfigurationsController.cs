using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Fabric;
using System.Linq;
using System.Threading.Tasks;
using Bullfrog.Actor.Interfaces;
using Bullfrog.Actor.Interfaces.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.ServiceFabric.Actors;

namespace Bullfrog.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ConfigurationsController : BaseManagementController
    {
        public ConfigurationsController(StatelessServiceContext statelessServiceContext)
            : base(statelessServiceContext)
        {
        }

        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesDefaultResponseType]
        [HttpGet]
        public async Task<List<string>> ListScaleGroups()
        {
            return await GetConfigurationManager().ListConfiguredScaleGroup(default);
        }

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

        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesDefaultResponseType]
        [HttpPut("{scaleGroup}")]
        public async Task<ActionResult> SetDefinition(string scaleGroup, ScaleGroupDefinition definition)
        {
            var validationResult = await GetConfigurationManager().ConfigureScaleGroup(scaleGroup, definition, default);
            if (validationResult != null && validationResult.Count > 0)
            {
                var result = new ValidationProblemDetails(validationResult);
                return BadRequest(result);
            }
            return NoContent();
        }

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
