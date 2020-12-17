using System;
using System.Diagnostics.CodeAnalysis;

namespace Bullfrog.Common
{
    [Serializable]
    [ExcludeFromCodeCoverage]
    public class ScaleGroupNotFoundException : BullfrogException
    {
        public ScaleGroupNotFoundException() { }
        public ScaleGroupNotFoundException(string message) : base(message) { }
        public ScaleGroupNotFoundException(string message, Exception inner) : base(message, inner) { }
        protected ScaleGroupNotFoundException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }
}
