using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace Bullfrog.Actors.Interfaces.Models.Validation
{
    /// <summary>
    /// Value validator.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
    public class ValueIsAttribute : ValidationAttribute
    {
        /// <summary>
        /// Creates an instance of the value validator.
        /// </summary>
        /// <param name="comparision">The type of comparasion which is used to check the validity of the value.</param>
        public ValueIsAttribute(ValueComparison comparision)
        {
            Comparision = comparision;
        }

        /// <summary>
        /// The type of comparasion which is used to check the validity of the value.
        /// </summary>
        public ValueComparison Comparision { get; }

        /// <summary>
        /// The other value which is compared to the validated value.
        /// </summary>
        public object Value { get; set; }

        /// <summary>
        /// The other property which is compared to the validated value.
        /// </summary>
        public string PropertyValue { get; set; }

        /// <inherit/>
        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            if (value == null)
                return ValidationResult.Success;

            var otherValue = GetValueToCompare(validationContext);
            if (otherValue is null)
                return ValidationResult.Success;
            if (otherValue.GetType() != value.GetType())
                otherValue = Convert.ChangeType(otherValue, value.GetType());

            var comparisionResult = Compare(value, otherValue);
            bool succeeded;
            string description;
            switch (Comparision)
            {
                case ValueComparison.LessThan:
                    succeeded = comparisionResult < 0;
                    description = "less than";
                    break;
                case ValueComparison.LessThanOrEqualTo:
                    succeeded = comparisionResult <= 0;
                    description = "less than or equal to";
                    break;
                case ValueComparison.EqualTo:
                    succeeded = comparisionResult == 0;
                    description = "equal to";
                    break;
                case ValueComparison.NotEqualTo:
                    succeeded = comparisionResult != 0;
                    description = "different than";
                    break;
                case ValueComparison.GreaterThanOrEqualTo:
                    succeeded = comparisionResult >= 0;
                    description = "greater than or equal to";
                    break;
                case ValueComparison.GreaterThen:
                    succeeded = comparisionResult > 0;
                    description = "greater than";
                    break;
                default:
                    throw new ArgumentException("Invalid comparison type.");
            }

            return succeeded
                ? ValidationResult.Success
                : new ValidationResult($"The value {value} is not {description} {otherValue}");
        }

        private int Compare(object value1, object value2)
        {
            var comparerClass = typeof(Comparer<>).MakeGenericType(value1.GetType());
            var defaultComparer = comparerClass.GetProperty("Default").GetValue(null);
            var compareMethod = defaultComparer.GetType().GetMethod("Compare");
            return (int)compareMethod.Invoke(defaultComparer, new[] { value1, value2 });
        }

        private object GetValueToCompare(ValidationContext validationContext)
        {
            if (PropertyValue != null)
            {
                var prop = validationContext.ObjectType.GetProperty(
                    PropertyValue,
                    BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.FlattenHierarchy);
                if (prop == null)
                {
                    throw new ArgumentException($"The property {PropertyValue} has not been found.");
                }

                return prop.GetValue(validationContext.ObjectInstance);
            }
            else if (Value != null)
            {
                return Value;
            }
            else
            {
                throw new ArgumentException($"Either {nameof(PropertyValue)} or {nameof(Value)} must be set to not null value.");
            }
        }
    }
}
