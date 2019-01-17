using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;

namespace Bullfrog.Common
{
    public static class CosmosClientExtensions
    {
        public static async Task SetProvisionedThrouputAsync(this CosmosClient client, int? throughput, string database, string container, CancellationToken cancellationToken = default)
        {
            var db = client.Databases[database];
            if (container == null)
            {
                if (!throughput.HasValue)
                {
                    throughput = await db.ReadProvisionedThroughputAsync(cancellationToken);
                    if (!throughput.HasValue)
                    {
                        throw new Exception($"The database {database} does not have provisioned throughput set.");
                    }
                }

                await db.ReplaceProvisionedThroughputAsync(throughput.Value, cancellationToken);
            }
            else
            {
                var cnt = db.Containers[container];

                if (!throughput.HasValue)
                {
                    throughput = await cnt.ReadProvisionedThroughputAsync(cancellationToken);
                    if (!throughput.HasValue)
                    {
                        throw new Exception($"The container {container} in the database {database} does not have provisioned throughput set.");
                    }
                }

                await cnt.ReplaceProvisionedThroughputAsync(throughput.Value, cancellationToken);
            }
        }
    }
}
