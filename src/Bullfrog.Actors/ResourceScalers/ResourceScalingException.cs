using System;

namespace Bullfrog.Actors.ResourceScalers
{

    [Serializable]
    public class ResourceScalingException : Exception
    {
        public ResourceScalingException() { }
        public ResourceScalingException(string message) : base(message) { }
        public ResourceScalingException(string message, Exception inner) : base(message, inner) { }
        protected ResourceScalingException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }
}
