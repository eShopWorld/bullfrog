using System;

namespace Bullfrog.Actors.Modules
{
    public class ScaleChangedEventArgs : EventArgs
    {
        public int Throughput { get; }

        public int? RequestedThroughput { get; }

        public ScaleChangedEventArgs(int throughput, int? requestedThroughput)
        {
            Throughput = throughput;
            RequestedThroughput = requestedThroughput;
        }
    }
}
