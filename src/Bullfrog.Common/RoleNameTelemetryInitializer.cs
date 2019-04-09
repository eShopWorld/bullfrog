using System;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.Extensibility;

namespace Bullfrog.Common
{
    /// <summary>
    /// Initializes the could_RoleName telemetry event value.
    /// </summary>
    public class RoleNameTelemetryInitializer : ITelemetryInitializer
    {
        private readonly string _roleName;

        /// <summary>
        /// Creates an instance of the intializer with the specified cloud_RoleName value.
        /// </summary>
        /// <param name="roleName">The role name value.</param>
        public RoleNameTelemetryInitializer(string roleName)
        {
            _roleName = roleName ?? throw new ArgumentNullException(nameof(roleName));
        }

        void ITelemetryInitializer.Initialize(ITelemetry telemetry)
        {
            if(telemetry.Context.Cloud.RoleName == null)
                telemetry.Context.Cloud.RoleName = _roleName;
        }
    }
}
