namespace Bullfrog.Common.Events
{
    public class AzureOperationDurationEvent : Eshopworld.Core.TimedTelemetryEvent
    {
        public string ResourceId { get; set; }

        public string Operation { get; set; }

        public string ExceptionMessage { get; set; }
    }
}
