using System;
using System.Collections.Generic;
using System.Text;

namespace Bullfrog.Actors.Helpers
{
    internal class CosmosThroughput
    {
        public int Throughput { get; set; }

        public int? MaxThroughputEverProvisioned { get; set; }

        public bool? IsThroughputChangePending { get; set; }

        public int? MinThroughput { get; set; }
    }
}
