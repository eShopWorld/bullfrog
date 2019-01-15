using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Bullfrog.Actor.Interfaces.Models.Validation
{
    public class ElementsHaveDistinctValuesAttribute : ValidationAttribute
    {
        public ElementsHaveDistinctValuesAttribute(string propertyName)
        {
            PropertyName = propertyName;
        }

        public string PropertyName { get; }


        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            var items = (System.Collections.IEnumerable)value;
            if (items == null)
            {
                return ValidationResult.Success;
            }

            var distinctValues = new HashSet<object>();
            var index = 0;
            foreach(var v in items)
            {
                if(v == null)
                {
                    return new ValidationResult("Item is required", new[] { $"[{index}]" });
                }

                var propertyValue = v.GetType().GetProperty(PropertyName)?.GetValue(v);
                if(propertyValue == null)
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
