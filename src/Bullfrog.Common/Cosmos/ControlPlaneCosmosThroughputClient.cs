using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Bullfrog.Common.Models;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Core;
using Microsoft.Azure.Management.ResourceManager.Fluent.Models;
using Newtonsoft.Json;

namespace Bullfrog.Common.Cosmos
{
    /// <summary>
    /// The client which uses the Azure control plane to read or change Cosmos DB throughput.
    /// </summary>
    public class ControlPlaneCosmosThroughputClient
    {
        static readonly Regex InvalidThroughputMessagePattern =
               new Regex("The offer should have valid throughput values between (?<min>\\d+) and (?<max>\\d+) inclusive in increments of \\d+",
                       RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private readonly string _resourceId;
        private readonly ResourceManagementClient _managementClient;

        public ControlPlaneCosmosThroughputClient(CosmosDbControlPlaneConnection connection, ResourceManagementClient managementClient)
        {
            _resourceId = $"{connection.AccountResurceId}/apis/sql/databases/{connection.DatabaseName}";
            if (!string.IsNullOrEmpty(connection.ContainerName))
                _resourceId += $"/containers/{connection.ContainerName}";
            _resourceId += "/settings/throughput";
            _managementClient = managementClient;
            var id = ResourceId.FromString(_resourceId);
            _managementClient.SubscriptionId = id.SubscriptionId;
        }

        public virtual async Task<int> Get()
        {
            using (var response = await _managementClient.Resources.GetByIdWithHttpMessagesAsync(_resourceId, "2015-04-08"))
            {
                return ParseThroughput(response.Body);
            }
        }

        public virtual async Task<int> Set(int throughput)
        {
            var parameters = new GenericResourceInner();
            parameters.Properties = new ThroughputChange(throughput);
            try
            {
                using (var response = await _managementClient.Resources.CreateOrUpdateByIdWithHttpMessagesAsync(_resourceId, "2015-04-08", parameters))
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

        private int ParseThroughput(GenericResourceInner genericResource)
            => ((Newtonsoft.Json.Linq.JObject)genericResource.Properties).Value<int>("throughput");

        private class ThroughputChange
        {
            public ThroughputChange()
            {
            }

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
