using System.Fabric;
using Bullfrog.Api.Models;
using Eshopworld.Core;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;

namespace Bullfrog.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ScaleGroupsController : ControllerBase
    {
        public ScaleGroupsController(IHostingEnvironment hostingEnvironment, IBigBrother bigBrother, StatelessServiceContext sfContext)
        {
        }

        [HttpGet("scaleGroup")]
        public ScaleGroupState GetCurrentState(string scaleGroup)
        {
            return new ScaleGroupState();
        }
    }
}
