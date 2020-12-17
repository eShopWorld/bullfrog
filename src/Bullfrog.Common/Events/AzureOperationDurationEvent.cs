using System.Diagnostics.CodeAnalysis;

namespace Bullfrog.Common.Events
{
    [ExcludeFromCodeCoverage]
    public class AzureOperationDurationEvent : Eshopworld.Core.TimedTelemetryEvent
    {
        public string ResourceId { get; set; }

        public string Operation { get; set; }

        public string ExceptionMessage { get; set; }
    }
}
