using Bullfrog.Actors.Interfaces.Models;

namespace Bullfrog.Actors.ResourceScalers
{
    /// <summary>
    /// Configuration of the <see cref="RunbookVmssScaler"/>
    /// </summary>
    public class RunbookVmssScalerConfiguration
    {
        /// <summary>
        /// The scale set managed by the scaler
        /// </summary>
        public ScaleSetConfiguration ScaleSet { get; set; }

        public string ScaleGroup { get; set; }

        public string Region { get; set; }

        /// <summary>
        /// The resource id of the automation account containing a runbook.
        /// </summary>
        public string AutomationAccountResourceId { get; set; }
    }
}
