using System;
using System.Threading;
using System.Threading.Tasks;
using Bullfrog.Actors.Interfaces.Models;
using Bullfrog.Common;
using Microsoft.Extensions.Configuration;
using CM = Microsoft.Azure.Cosmos;

namespace Bullfrog.Actors.Helpers
{
    internal class CosmosManager : ICosmosManager
    {
        private readonly IConfigurationRoot _configuration;

        public CosmosManager(IConfigurationRoot configuration)
        {
            _configuration = configuration;
        }

        public async Task<int> Reset(CosmosConfiguration configuration, CancellationToken cancellationToken = default)
        {
            using (var client = new CM.CosmosClient(GetConnectionString(configuration)))
            {
                var throughput = configuration.MinimumRU;
                await client.SetProvisionedThrouputAsync(throughput, configuration.DatabaseName, configuration.ContainerName);
                return throughput;
            }
        }

        public async Task<int> SetScale(int requestedScale, CosmosConfiguration configuration, CancellationToken cancellationToken = default)
        {
            var ruCount = Math.Ceiling(requestedScale * configuration.RequestUnitsPerRequest);
            if (ruCount > configuration.MaximumRU)
            {
                ruCount = configuration.MaximumRU;
            }
            else if (ruCount < configuration.MinimumRU)
            {
                ruCount = configuration.MinimumRU;
            }

            int throughput = ((int)ruCount + 99) / 100 * 100;

            using (var client = new CM.CosmosClient(GetConnectionString(configuration)))
            {
                await client.SetProvisionedThrouputAsync(throughput, configuration.DatabaseName, configuration.ContainerName);
            }

            return throughput;
        }

        private string GetConnectionString(CosmosConfiguration configuration)
        {
            var connectionString = _configuration.GetCosmosAccountConnectionString(configuration.AccountName);
            if (connectionString == null)
            {
                _configuration.Reload();
                connectionString = _configuration.GetCosmosAccountConnectionString(configuration.AccountName);
            }
            if (connectionString == null)
            {
                throw new ArgumentException($"The connection string for the Cosmos account {configuration.AccountName} has not been found");
            }

            return connectionString;
        }
    }
}
