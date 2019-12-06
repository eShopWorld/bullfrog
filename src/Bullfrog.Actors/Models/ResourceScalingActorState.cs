using System;

namespace Bullfrog.Actors.Models
{
    internal sealed class ResourceScalingActorState
    {
        //public ResourceThroughput? Requested { get; set; }

        //public ResourceThroughput? Configured { get; set; }

        //public DateTimeOffset NextCheck { get; set; }

        public int? RequestedThroughput { get; set; }

        public DateTimeOffset? RequestedEndsAt { get; set; }

        //public int ConfiguredThroughput { get; set; }

        //public DateTimeOffset? ConfiguredEndsAt { get; set; }

        public bool OperationCompleted { get; set; }

        public int? FinalThroughput { get; set; }

        public string ResourceScalerState { get; set; }
    }

    internal struct ResourceThroughput : IEquatable<ResourceThroughput>
    {
        public int Throughput { get; set; }

        public DateTimeOffset EndsAt { get; set; }

        public ResourceThroughput(int throughput, DateTimeOffset endsAt)
        {
            Throughput = throughput;
            EndsAt = endsAt;
        }

        public override bool Equals(object obj)
            => obj is ResourceThroughput && Equals((ResourceThroughput)obj);

        public bool Equals(ResourceThroughput other)
            => Throughput == other.Throughput && EndsAt == other.EndsAt;

        public override int GetHashCode() => HashCode.Combine(Throughput, EndsAt);

        public static bool operator ==(ResourceThroughput rt1, ResourceThroughput rt2)
            => rt1.Equals(rt2);
        public static bool operator !=(ResourceThroughput rt1, ResourceThroughput rt2)
            => !(rt1 == rt2);
    }
}
