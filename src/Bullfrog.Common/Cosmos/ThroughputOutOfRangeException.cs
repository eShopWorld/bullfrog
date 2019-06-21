using System;
using System.Runtime.Serialization;

namespace Bullfrog.Common.Cosmos
{
    [Serializable]
    public class ThroughputOutOfRangeException : BullfrogException
    {
        public int MinimumThroughput { get; }

        public int MaximumThroughput { get; }

        public ThroughputOutOfRangeException(int minimumThroughput, int maximumThroughput, Exception inner)
            : base($"The new throughput value should be between {minimumThroughput} and {maximumThroughput} inclusive.", inner)
        {
            MinimumThroughput = minimumThroughput;
            MaximumThroughput = maximumThroughput;
        }
        protected ThroughputOutOfRangeException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
            MinimumThroughput = info.GetInt32(nameof(MinimumThroughput));
            MaximumThroughput = info.GetInt32(nameof(MaximumThroughput));
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue(nameof(MinimumThroughput), MinimumThroughput);
            info.AddValue(nameof(MaximumThroughput), MaximumThroughput);
            base.GetObjectData(info, context);
        }
    }
}
