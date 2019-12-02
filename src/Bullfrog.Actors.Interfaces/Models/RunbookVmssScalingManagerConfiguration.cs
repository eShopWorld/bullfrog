using System.Collections.Generic;

namespace Bullfrog.Actors.Interfaces.Models
{
    /// <summary>
    /// The RunbookVmssScalingManager's configuration.
    /// </summary>
    public class RunbookVmssScalingManagerConfiguration
    {
        /// <summary>
        /// The resource id of an automation account used to executed the runbook.
        /// </summary>
        public string AutomationAccountResourceId { get; set; }

        /// <summary>
        /// The configuration of VMSS.
        /// </summary>
        public ScaleSetConfiguration VmssConfiguration { get; set; }
    }
}
