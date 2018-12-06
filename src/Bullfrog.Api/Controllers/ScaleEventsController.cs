using System;
using System.Collections.Generic;
using System.Fabric;
using Bullfrog.Api.Models;
using Eshopworld.Core;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;

namespace Bullfrog.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ScaleEventsController : ControllerBase
    {
        public ScaleEventsController(IHostingEnvironment hostingEnvironment, IBigBrother bigBrother, StatelessServiceContext sfContext)
        {
        }

        [HttpGet("{scaleGroup}")]
        public IEnumerable<ScheduledScaleEvent> ListScheduledEvents(string scaleGroup)
        {
            throw new NotImplementedException();
        }

        [HttpGet("{scaleGroup}/{eventId}")]
        public ScheduledScaleEvent ListScheduledEvents(string scaleGroup, Guid eventId)
        {
            throw new NotImplementedException();
        }

        [HttpPut("{scaleGroup}/{eventId}")]
        public ActionResult SaveScaleEvent(string scaleGroup, Guid eventId, ScaleEvent scaleEvent)
        {
            throw new NotImplementedException();
        }

        [HttpDelete("{scaleGroup}/{eventId}")]
        public ActionResult DeleteScaleEvent(string scaleGroup, Guid eventId)
        {
            throw new NotImplementedException();
        }
    }
}
