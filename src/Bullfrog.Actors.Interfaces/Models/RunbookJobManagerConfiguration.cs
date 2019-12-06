namespace Bullfrog.Actors.Interfaces.Models
{
    /// <summary>
    /// Defines the details of the actor configuration.
    /// </summary>
    public class RunbookJobManagerConfiguration
    {
        /// <summary>
        /// The resource id of the automation account.
        /// </summary>
        public string AutomationAccountResourceId { get; set; }

        /// <summary>
        /// The name of the runbook.
        /// </summary>
        public string RunbookName { get; set; }
    }
}
