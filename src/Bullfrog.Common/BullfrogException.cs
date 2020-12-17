using System;
using System.Diagnostics.CodeAnalysis;

namespace Bullfrog.Common
{

    [Serializable]
    [ExcludeFromCodeCoverage]
    public class BullfrogException : Exception
    {
        public BullfrogException() { }
        public BullfrogException(string message) : base(message) { }
        public BullfrogException(string message, Exception inner) : base(message, inner) { }
        protected BullfrogException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }
}
