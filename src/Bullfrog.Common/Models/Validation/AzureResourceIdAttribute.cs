using System;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Microsoft.Azure.Management.ResourceManager.Fluent.Core;

namespace Bullfrog.Common.Models.Validation
{
    /// <summary>
    /// Azure ResourceId format validation attribute
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false)]
    public class AzureResourceIdAttribute : ValidationAttribute
    {
        /// <summary>
        /// Checks whether value has a valid Azure ResourceId format.
        /// </summary>
        /// <param name="value">The value to test for validity.</param>
        /// <returns><c>true</c> if the given value can be valid ResourceId, </returns>
        public override bool IsValid(object value)
        {
            string stringValue = Convert.ToString(value, CultureInfo.CurrentCulture);
            if (string.IsNullOrWhiteSpace(stringValue))
                return true;

            try
            {
                ResourceId.FromString(stringValue);
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }
    }
}
