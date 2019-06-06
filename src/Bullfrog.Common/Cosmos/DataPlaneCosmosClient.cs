using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;


namespace Bullfrog.Common.Cosmos
{

    internal class DataPlaneCosmosClient : CosmosThroughputClientBase
    {
        private readonly IConfigurationRoot _configuration;
        private readonly CosmosDbConfiguration _dbConfiguration;
        private readonly DocumentClient _client;


        public DataPlaneCosmosClient(IConfigurationRoot configuration, CosmosDbConfiguration dbConfiguration)
        {
            _configuration = configuration;
            _dbConfiguration = dbConfiguration;
        }

        public override Task<CosmosThroughput> Get()
        {
            throw new NotImplementedException();
        }

        public override Task<CosmosThroughput> Set(int throughput)
        {
            throw new NotImplementedException();
        }
    }
}
