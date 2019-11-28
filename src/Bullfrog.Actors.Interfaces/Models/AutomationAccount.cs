using System.ComponentModel.DataAnnotations;
using Bullfrog.Common.Models.Validation;

namespace Bullfrog.Actors.Interfaces.Models
{
    /// <summary>
    /// Defines the automation account resourceId and the name used to identify it.
    /// </summary>
    public class AutomationAccount
    {
        /// <summary>
        /// The unique name of the automation account.
        /// </summary>
        [Required]
        public string Name { get; set; }

        /// <summary>
        /// The resource id of the automation account.
        /// </summary>
        [Required]
        [AzureResourceId]
        public string ResourceId { get; set; }
    }
}
