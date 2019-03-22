using System;
using System.Collections.Generic;
using System.Text;

namespace Bullfrog.Actors.EventModels
{
    public class AzureOperationDurationEvent : Eshopworld.Core.TimedTelemetryEvent
    {
        public string ResourceId { get; set; }

        public string Operation { get; set; }
    }
}
