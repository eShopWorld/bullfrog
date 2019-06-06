using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Bullfrog.Actors.EventModels;
using Bullfrog.Common;
using Eshopworld.Core;
using Microsoft.Azure.Cosmos;

namespace Bullfrog.Actors.Helpers
{
    internal sealed class DataPlaneThroughtputManager : ICosmosThroughputManager
    {
        static readonly Regex InvalidThroughputMessagePattern =
           new Regex("The offer should have valid throughput values between (?<min>\\d+) and (?<max>\\d+) inclusive in increments of \\d+",
                   RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private readonly string _connectionString;
        private readonly string _accountName;
        private readonly string _databaseName;
        private readonly string _containerName;
        private readonly IBigBrother _bigBrother;
        private CosmosClient _client;

        public DataPlaneThroughtputManager(string connectionString, string accountName, string databaseName, string containerName, IBigBrother bigBrother)
        {
            _connectionString = connectionString;
            _accountName = accountName;
            _databaseName = databaseName;
            _containerName = containerName;
            _bigBrother = bigBrother;
        }


        public void Dispose()
        {
            if(_client != null)
            {
                _client.Dispose();
                _client = null;
            }
        }

        public async Task<CosmosThroughput> GetThroughput()
        {
            var client = GetClient();

            var throughput = await client.GetProvisionedThroughputAsync(_databaseName, _containerName);
            if(!throughput.HasValue)
            {
                throw new BullfrogException($"Throughput of {_accountName} is configured at the expected level.");
            }

            return new CosmosThroughput
            {
                Throughput = throughput.Value,
            };
        }

        public async Task<int> SetThroughput(int throughput)
        {
            var client = GetClient();
            try
            {
                await client.SetProvisionedThrouputAsync(throughput, _databaseName, _containerName);
            }
            catch (CosmosException ex) when (InvalidThroughputMessagePattern.IsMatch(ex.Message))
            {
                // A simple workaround for an issue with minimum throughput.
                // CosmosClient does not allow currently to get the minimal throughput in any other way.
                // This issue is tracked by https://github.com/Azure/azure-cosmos-dotnet-v3/issues/254 .
                var match = InvalidThroughputMessagePattern.Match(ex.Message);
                var minThroughput = int.Parse(match.Groups["min"].Value, System.Globalization.CultureInfo.InvariantCulture);
                if (throughput >= minThroughput)
                    throw;

                _bigBrother.Publish(new CosmosThroughputTooLow
                {
                    CosmosAccunt = _accountName,
                    Container = _containerName,
                    Database = _databaseName,
                    ErrorMessage = ex.Message,
                    MinThroughput = minThroughput,
                    ThroughputRequired = throughput,
                });

                await client.SetProvisionedThrouputAsync(minThroughput, _databaseName, _containerName);
                throughput = minThroughput;
            }

            return throughput;
        }

        private CosmosClient GetClient()
        {
            if(_client is null)
            {
                _client = new CosmosClient(_connectionString);
            }

            return _client;
        }
    }
}
