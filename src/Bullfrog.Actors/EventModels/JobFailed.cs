namespace Bullfrog.Actors.EventModels
{
    public class JobFailed : JobTelemetryEvent
    {
        public string Exception { get; set; }

        public string ProvisioningState { get; set; }

        public string Status { get; set; }
    }
}
