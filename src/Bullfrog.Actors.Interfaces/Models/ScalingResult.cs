using System;
using System.Collections.Generic;
using System.Text;

namespace Bullfrog.Actors.Interfaces.Models
{
    public class ScalingResult<T>
    {
        public T Value { get; set; }

        public string ExceptionMessage { get; set; }
    }

    public static class ScalingResult
    {
        public static ScalingResult<T> FromValue<T>(T value, string exceptionMessage = default)
            => new ScalingResult<T> { Value = value, ExceptionMessage = exceptionMessage, };
    }
}
