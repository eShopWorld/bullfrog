using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Eshopworld.Core;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Core;

namespace Bullfrog.Actors.Helpers
{
    class ControlPlaneThroughputManager : ICosmosThroughputManager
    {
        private const string ApiVersion = "2015-04-08";
        private readonly IResourceManagementClient _resourceManagementClient;
        private readonly IBigBrother _bigBrother;

        public ControlPlaneThroughputManager(IResourceManagementClient resourceManagementClient, IBigBrother bigBrother)
        {
            _resourceManagementClient = resourceManagementClient;
            _bigBrother = bigBrother;
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public async Task<CosmosThroughput> GetThroughput(string accountName, string cosmosDbResourceId, string databaseName, string containerName)
        {
            var client = GetClientForResource(cosmosDbResourceId);
            var response = await client.Resources.GetByIdAsync(resourceId, ApiVersion)
        }

        public Task<int> SetThroughput(int throughput, string accountName, string cosmosDbResourceId, string databaseName, string containerName)
        {
            throw new NotImplementedException();
        }

        private IResourceManagementClient GetClientForResource(string resourceId)
        {
            var id = ResourceId.FromString(resourceId);
            var client = _resourceManagementClient;
            client.SubscriptionId = id.SubscriptionId;
            return client;
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

            [Newtonsoft.Json.JsonProperty(PropertyName = "resource")]
            public ThroughputValue Resource { get; set; }
        }

        private class ThroughputValue
        {
            [Newtonsoft.Json.JsonProperty(PropertyName = "throughput")]
            public int Throughput { get; set; }
        }
    }
}
