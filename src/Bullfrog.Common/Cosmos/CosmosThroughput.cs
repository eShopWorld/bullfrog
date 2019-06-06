namespace Bullfrog.Common.Cosmos
{
    public class CosmosThroughput
    {
        public int RequestsUnits { get; set; }

        public int MaxRequestUnitsEverProvisioned { get; set; }

        public bool IsThroughputChangePending { get; set; }

        public int MinimalRequestUnits { get; set; }
    }
}
