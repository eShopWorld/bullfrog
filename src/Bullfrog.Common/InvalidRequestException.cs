using System;
using System.Diagnostics.CodeAnalysis;

namespace Bullfrog.Common
{
    [Serializable]
    [ExcludeFromCodeCoverage]
    public class InvalidRequestException : BullfrogException
    {
        public InvalidRequestException() { }
        public InvalidRequestException(string message) : base(message) { }
        public InvalidRequestException(string message, Exception inner) : base(message, inner) { }
        protected InvalidRequestException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }
}
