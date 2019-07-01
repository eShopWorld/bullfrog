using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Bullfrog.Common.Models;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Core;
using Microsoft.Azure.Management.ResourceManager.Fluent.Models;
using Newtonsoft.Json;

namespace Bullfrog.Common.Cosmos
{
    public static class ControlPlaneCosmosThroughputExtensionMethods
    {
        static readonly Regex InvalidThroughputMessagePattern =
                new Regex("The offer should have valid throughput values between (?<min>\\d+) and (?<max>\\d+) inclusive in increments of \\d+",
                        RegexOptions.Compiled | RegexOptions.CultureInvariant);


        public static async Task<int> GetThroughput(this IResourceManagementClient managementClient, CosmosDbControlPlaneConnection connection)
        {
            var resourceId = ConfigureManagementClient(managementClient, connection);
            using (var response = await managementClient.Resources.GetByIdWithHttpMessagesAsync(resourceId, "2015-04-08"))
            {
                return ParseThroughput(response.Body);
            }
        }

        public static async Task<int> SetThroughput(this IResourceManagementClient managementClient, int newThroughput, CosmosDbControlPlaneConnection connection)
        {
            var resourceId = ConfigureManagementClient(managementClient, connection);
            var parameters = new GenericResourceInner
            {
                Properties = new ThroughputChange(newThroughput)
            };
            try
            {
                using (var response = await managementClient.Resources.CreateOrUpdateByIdWithHttpMessagesAsync(resourceId, "2015-04-08", parameters))
                {
                    return ParseThroughput(response.Body);
                }
            }
            catch (Microsoft.Rest.Azure.CloudException ex) when (InvalidThroughputMessagePattern.IsMatch(ex.Message))
            {
                // The control plane does not provide any other way to get minimal throughput.
                var match = InvalidThroughputMessagePattern.Match(ex.Message);
                var minThroughput = int.Parse(match.Groups["min"].Value, System.Globalization.CultureInfo.InvariantCulture);
                var maxThroughput = int.Parse(match.Groups["max"].Value, System.Globalization.CultureInfo.InvariantCulture);
                throw new ThroughputOutOfRangeException(minThroughput, maxThroughput, ex);
            }
        }

        private static string ConfigureManagementClient(IResourceManagementClient managementClient, CosmosDbControlPlaneConnection connection)
        {
            var resourceId = $"{connection.AccountResurceId}/apis/sql/databases/{connection.DatabaseName}";
            if (!string.IsNullOrEmpty(connection.ContainerName))
                resourceId += $"/containers/{connection.ContainerName}";
            resourceId += "/settings/throughput";
            var id = ResourceId.FromString(resourceId);
            managementClient.SubscriptionId = id.SubscriptionId;
            return resourceId;
        }

        private static int ParseThroughput(GenericResourceInner genericResource)
                => ((Newtonsoft.Json.Linq.JObject)genericResource.Properties).Value<int>("throughput");

        private class ThroughputChange
        {
            public ThroughputChange(int throughput)
            {
                Resource = new ThroughputValue
                {
                    Throughput = throughput,
                };
            }

            [JsonProperty(PropertyName = "resource")]
            public ThroughputValue Resource { get; set; }
        }

        private class ThroughputValue
        {
            [JsonProperty(PropertyName = "throughput")]
            public int Throughput { get; set; }
        }
    }
}
