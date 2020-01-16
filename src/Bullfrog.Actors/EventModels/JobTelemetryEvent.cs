using System;
using Eshopworld.Core;

namespace Bullfrog.Actors.EventModels
{
    public abstract class JobTelemetryEvent : TelemetryEvent
    {
        public string AutomationAccountResourceId { get; set; }

        public string RunbookName { get; set; }

        public string Vmss { get; set; }

        public Guid JobId { get; set; }
    }
}
