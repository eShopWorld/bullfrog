namespace Bullfrog.Actors.Interfaces.Models.Validation
{
    /// <summary>
    /// Defines the comparison method of value validator.
    /// </summary>
    public enum ValueComparison
    {
        /// <summary>
        /// The value must be less than the other value.
        /// </summary>
        LessThan,

        /// <summary>
        /// The value must be less than or equal to the other value.
        /// </summary>
        LessThanOrEqualTo,

        /// <summary>
        /// The value must be equal to the other value.
        /// </summary>
        EqualTo,

        /// <summary>
        /// The value must not be equal to the other value.
        /// </summary>
        NotEqualTo,

        /// <summary>
        /// The value must be greater than or equal to the other value.
        /// </summary>
        GreaterThanOrEqualTo,

        /// <summary>
        /// The value must be greater than the other value.
        /// </summary>
        GreaterThen
    }
}
