namespace Bullfrog.Actors.Interfaces.Models
{
    /// <summary>
    /// Represents a serializable result of the scaling operation.
    /// </summary>
    /// <typeparam name="T">The type of scaling operation result.</typeparam>
    public class ScalingResult<T>
    {
        /// <summary>
        /// The scaling operation result.
        /// </summary>
        public T Value { get; set; }
        
        /// <summary>
        /// The optional error message.
        /// </summary>
        public string ExceptionMessage { get; set; }
    }

    /// <summary>
    /// A helper class to create <see cref="ScalingResult{T}"/> instances.
    /// </summary>
    public static class ScalingResult
    {
        /// <summary>
        /// Create a scaling result based on the given value.
        /// </summary>
        /// <typeparam name="T">The type of the result.</typeparam>
        /// <param name="value">The value of the result.</param>
        /// <param name="exceptionMessage">The optional error message.</param>
        /// <returns>The new instance of the result.</returns>
        public static ScalingResult<T> FromValue<T>(T value, string exceptionMessage = default)
            => new ScalingResult<T> { Value = value, ExceptionMessage = exceptionMessage, };
    }
}
