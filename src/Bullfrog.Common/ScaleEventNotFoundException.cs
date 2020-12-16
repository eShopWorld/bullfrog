using System;
using System.Diagnostics.CodeAnalysis;

namespace Bullfrog.Common
{
    [Serializable]
    [ExcludeFromCodeCoverage]
    public class ScaleEventNotFoundException : BullfrogException
    {
        public ScaleEventNotFoundException() { }
        public ScaleEventNotFoundException(string message) : base(message) { }
        public ScaleEventNotFoundException(string message, Exception inner) : base(message, inner) { }
        protected ScaleEventNotFoundException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }
}
