using System.Threading.Tasks;
using Bullfrog.Common.Models;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Core;
using Newtonsoft.Json;

namespace Bullfrog.Common.Cosmos
{
    /// <summary>
    /// The client which uses the Azure control plane to read or change Cosmos DB throughput.
    /// </summary>
    public class ControlPlaneCosmosThroughputClient
    {
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
                var throughput = ((Newtonsoft.Json.Linq.JObject)response.Body.Properties).Value<int>("throughput");
                return throughput;
            }
        }

        public virtual async Task Set(int throughput)
        {
            var parameters = new Microsoft.Azure.Management.ResourceManager.Fluent.Models.GenericResourceInner();
            parameters.Properties = new ThroughputChange(throughput);
            using (var response = await _managementClient.Resources.CreateOrUpdateByIdWithHttpMessagesAsync(_resourceId, "2015-04-08", parameters))
            {
            }
        }

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
