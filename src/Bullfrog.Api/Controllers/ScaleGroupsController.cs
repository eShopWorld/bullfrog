using System;
using System.Collections.Generic;
using System.Fabric;
using System.Linq;
using System.Threading.Tasks;
using Bullfrog.Actor.Interfaces;
using Bullfrog.Api.Models;
using Eshopworld.Core;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;

namespace Bullfrog.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ScaleGroupsController : BaseManagementController
    {
        public ScaleGroupsController(IHostingEnvironment hostingEnvironment, IBigBrother bigBrother, StatelessServiceContext sfContext)
            : base(sfContext)
        {
        }

        [HttpGet("scaleGroup")]
        public async Task<ScaleGroupState> GetCurrentState(string scaleGroup)
        {
            var regions = ListRegionsOfScaleGroup(scaleGroup);

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
            for (int i = 0; i < regions.Length; i++)
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
