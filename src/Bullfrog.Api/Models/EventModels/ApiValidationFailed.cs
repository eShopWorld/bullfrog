using Eshopworld.Core;

namespace Bullfrog.Api.Models.EventModels
{
    /// <summary>
    /// Reports API validation errors.
    /// </summary>
    public class ApiValidationFailed : TelemetryEvent
    {
        /// <summary>
        /// The serialized validation error.
        /// </summary>
        public string ValidationError { get; set; }
    }
}
