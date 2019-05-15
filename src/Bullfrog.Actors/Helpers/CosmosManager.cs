using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Bullfrog.Actors.EventModels;
using Bullfrog.Actors.Interfaces.Models;
using Bullfrog.Common;
using Eshopworld.Core;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;

namespace Bullfrog.Actors.Helpers
{
    internal class CosmosManager : ICosmosManager
    {
        static readonly Regex InvalidThroughputMessagePattern =
            new Regex("The offer should have valid throughput values between (?<min>\\d+) and (?<max>\\d+) inclusive in increments of \\d+",
                    RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private readonly IConfigurationRoot _configuration;
        private readonly IBigBrother _bigBrother;

        public CosmosManager(IConfigurationRoot configuration, IBigBrother bigBrother)
        {
            _configuration = configuration;
            _bigBrother = bigBrother;
        }

        public async Task<int> Reset(CosmosConfiguration configuration, CancellationToken cancellationToken = default)
        {
            return await SetThroughput(configuration, configuration.MinimumRU);
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

            return await SetThroughput(configuration, throughput);
        }

        private async Task<int> SetThroughput(CosmosConfiguration configuration, int throughput)
        {
            using (var client = new CosmosClient(GetConnectionString(configuration)))
            {
                try
                {
                    await client.SetProvisionedThrouputAsync(throughput, configuration.DatabaseName, configuration.ContainerName);
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
                        CosmosAccunt = configuration.AccountName,
                        Container = configuration.ContainerName,
                        Database = configuration.DatabaseName,
                        ErrorMessage = ex.Message,
                        MinThroughput = minThroughput,
                        ThroughputRequired = throughput,
                    });

                    await client.SetProvisionedThrouputAsync(minThroughput, configuration.DatabaseName, configuration.ContainerName);
                    throughput = minThroughput;
                }
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
