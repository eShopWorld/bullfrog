using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bullfrog.Api.Controllers
{
    /// <summary>
    /// probe controller
    /// </summary>
    [Route("/probe")]
    [AllowAnonymous]
    public class ProbeController : Controller
    {
        /// <summary>
        /// probe endpoint
        /// </summary>
        [HttpGet]
        public IActionResult Get()
        {
            return Ok();
        }
    }
}
