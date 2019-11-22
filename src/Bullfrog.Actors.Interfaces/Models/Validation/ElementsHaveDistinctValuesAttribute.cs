using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Bullfrog.Actors.Interfaces.Models.Validation
{
    /// <summary>
    /// Validates tthat the property contains a list of items with distinct values.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
    public class ElementsHaveDistinctValuesAttribute : ValidationAttribute
    {
        /// <summary>
        /// Creates an instance of the validator.
        /// </summary>
        /// <param name="propertyName">The item's property used to test for uniqueness.</param>
        public ElementsHaveDistinctValuesAttribute(string propertyName)
        {
            PropertyName = propertyName;
        }

        /// <summary>
        /// The name of the item's property which is checked for uniqueness.
        /// </summary>
        public string PropertyName { get; }

        /// <inherit/>
        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            if (value == null)
            {
                return ValidationResult.Success;
            }

            if (!(value is System.Collections.IEnumerable items))
            {
                return new ValidationResult("Value is not a collection");
            }

            var distinctValues = new HashSet<object>();
            var index = 0;
            foreach (var v in items)
            {
                if (v == null)
                {
                    return new ValidationResult("Value is required", new[] { $"[{index}]" });
                }

                var propertyValue = v.GetType().GetProperty(PropertyName)?.GetValue(v);
                if (propertyValue == null)
                {
                    return new ValidationResult("Value is missing.", new[] { $"[{index}].{PropertyName}" });
                }

                if (!distinctValues.Add(propertyValue))
                {
                    return new ValidationResult($"Value {propertyValue} is used more than once.", new[] { $"[{index}].{PropertyName}" });
                }

                index++;
            }

            return ValidationResult.Success;
        }
    }
}
