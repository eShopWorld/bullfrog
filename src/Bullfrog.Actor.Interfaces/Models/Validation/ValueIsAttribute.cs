using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace Bullfrog.Actors.Interfaces.Models.Validation
{
    [AttributeUsage(AttributeTargets.Property)]
    public class ValueIsAttribute : ValidationAttribute
    {
        public ValueIsAttribute(ValueComparision comparision)
        {
            Comparision = comparision;
        }

        public ValueComparision Comparision { get; }

        public object Value { get; set; }

        public string PropertyValue { get; set; }

        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            if (value == null)
            {
                return ValidationResult.Success;
            }

            var otherValue = GetValueToCompare(value, validationContext);
            if (otherValue.GetType() != value.GetType())
            {
                otherValue = Convert.ChangeType(otherValue, value.GetType());
            }

            var comparisionResult = Compare(value, otherValue);
            bool succeeded;
            string description;
            switch (Comparision)
            {
                case ValueComparision.LessThan:
                    succeeded = comparisionResult < 0;
                    description = "less than";
                    break;
                case ValueComparision.LessThanOrEqualTo:
                    succeeded = comparisionResult <= 0;
                    description = "less than or equal to";
                    break;
                case ValueComparision.EqualTo:
                    succeeded = comparisionResult == 0;
                    description = "equal to";
                    break;
                case ValueComparision.NotEqualTo:
                    succeeded = comparisionResult != 0;
                    description = "different than";
                    break;
                case ValueComparision.GreaterThanOrEqualTo:
                    succeeded = comparisionResult >= 0;
                    description = "greater than or equal to";
                    break;
                case ValueComparision.GreaterThen:
                    succeeded = comparisionResult > 0;
                    description = "greater than";
                    break;
                default:
                    throw new Exception("Invalid comparasion type.");
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

        private object GetValueToCompare(object value, ValidationContext validationContext)
        {
            if (PropertyValue != null)
            {
                var prop = validationContext.ObjectType.GetProperty(
                    PropertyValue,
                    BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.FlattenHierarchy);
                if (prop == null)
                {
                    throw new Exception($"The property {PropertyValue} has not been found.");
                }

                return prop.GetValue(validationContext.ObjectInstance) ?? throw new Exception($"The property {PropertyValue} returned null");
            }
            else if (Value != null)
            {
                return Value;
            }
            else
            {
                throw new Exception($"Either {nameof(PropertyValue)} or {nameof(Value)} must be set to not null value.");
            }
        }
    }

    public enum ValueComparision
    {
        LessThan,
        LessThanOrEqualTo,
        EqualTo,
        NotEqualTo,
        GreaterThanOrEqualTo,
        GreaterThen
    }
}
