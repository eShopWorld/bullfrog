using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;

namespace Bullfrog.Common
{
    /// <summary>
    /// The Cosmos DB related extension methods.
    /// </summary>
    public static class CosmosClientExtensions
    {
        /// <summary>
        /// Sets the provisioned thoughput of database or a container.
        /// </summary>
        /// <param name="client">The Cosmos DB client.</param>
        /// <param name="throughput">The requested throughput (in RUs)</param>
        /// <param name="database">The CosomosDB database name.</param>
        /// <param name="container">The optional container name. The provisioned throuput will be set at the container level is this name is provided.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
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
                        throw new BullfrogException($"The database {database} does not have provisioned throughput set.");
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
                        throw new BullfrogException($"The container {container} in the database {database} does not have provisioned throughput set.");
                    }
                }

                await cnt.ReplaceProvisionedThroughputAsync(throughput.Value, cancellationToken);
            }
        }

        /// <summary>
        /// Reads the provisioned throughput
        /// </summary>
        /// <param name="client">The Cosmos DB client.</param>
        /// <param name="database">The CosomosDB database name.</param>
        /// <param name="container">The optional container name. The provisioned throuput set at the container level is returned if this name is provided.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        public static async Task<int?> GetProvisionedThroughputAsync(this CosmosClient client, string database, string container, CancellationToken cancellationToken = default)
        {
            var db = client.Databases[database];
            if (container == null)
            {
                return await db.ReadProvisionedThroughputAsync(cancellationToken);
            }
            else
            {
                var cosmosContainer = db.Containers[container];
                return await cosmosContainer.ReadProvisionedThroughputAsync(cancellationToken);
            }
        }
    }
}
