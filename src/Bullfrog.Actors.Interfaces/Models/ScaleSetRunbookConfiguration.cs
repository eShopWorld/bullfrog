using System.ComponentModel.DataAnnotations;

namespace Bullfrog.Actors.Interfaces.Models
{
    /// <summary>
    /// Defines parameters necessary to execute an automation runbook.
    /// </summary>
    public class ScaleSetRunbookConfiguration
    {
        /// <summary>
        /// Name of the automation account (defined in <see cref="ScaleGroupDefinition.AutomationAccounts"/>).
        /// </summary>
        [Required]
        public string AutomationAccountName { get; set; }

        /// <summary>
        /// The name of the runbook.
        /// </summary>
        [Required]
        public string RunbookName { get; set; }

        /// <summary>
        /// The optional name of Vmss to scale.
        /// </summary>
        /// <remarks>
        /// If it's missing the <see cref="ScaleSetConfiguration.Name"/> is used.
        /// </remarks>
        public string ScaleSetName { get; set; }
    }
}
