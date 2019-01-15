namespace Bullfrog.Actor.Interfaces.Models
{
    public class CosmosConfiguration
    {
        public string AccountName { get; set; }

        public string DatabaseName { get; set; }

        public string ContainerName { get; set; }

        public double RequestsPerRU { get; set; }

        public int MinimumRU { get; set; }

        public int MaximumRU { get; set; }
    }
}
