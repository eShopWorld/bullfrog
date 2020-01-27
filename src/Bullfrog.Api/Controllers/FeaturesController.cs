using System.ComponentModel.DataAnnotations;
using System.Fabric;
using System.Threading.Tasks;
using Bullfrog.Common.Models;
using Eshopworld.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.ServiceFabric.Actors.Client;

namespace Bullfrog.Api.Controllers
{
    /// <summary>
    /// Manages feature flags.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Policy = AuthenticationPolicies.AdminScope)]
    public class FeaturesController : BaseManagementController
    {
        private readonly IBigBrother _bigBrother;

        /// <summary>
        /// Creates an instance of <see cref="ConfigurationsController"/>.
        /// </summary>
        /// <param name="statelessServiceContext">The instance of <see cref="StatelessServiceContext"/>.</param>
        /// <param name="proxyFactory">A factory used to create actor proxies.</param>
        /// <param name="bigBrother">Telemetry client.</param>
        public FeaturesController(StatelessServiceContext statelessServiceContext, IActorProxyFactory proxyFactory, IBigBrother bigBrother)
            : base(statelessServiceContext, proxyFactory)
        {
            _bigBrother = bigBrother;
        }

        /// <summary>
        /// Gets the current feature flags.
        /// </summary>
        /// <returns></returns>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesDefaultResponseType]
        [HttpGet]
        public async Task<FeatureFlagsConfiguration> GetFeatureFlags()
        {
            return await GetConfigurationManager().GetFeatureFlags();
        }

        /// <summary>
        /// Configures feature flags.
        /// </summary>
        /// <param name="featureFlags">New feature flags</param>
        /// <returns>The updated feature flags</returns>
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesDefaultResponseType]
        [HttpPut]
        public async Task<FeatureFlagsConfiguration> SetFeatures([Required]FeatureFlagsConfiguration featureFlags)
        {
            return await GetConfigurationManager().SetFeatureFlags(featureFlags);
        }
    }
}
