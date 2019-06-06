using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Configuration;
using System.Text;
using System.Threading;
using System.Threading.Tasks;


namespace Bullfrog.Common.Cosmos
{

    internal interface ICosmosThroughputClientInitializer : ICosmosThroughputClient
    {
        void UseConfiguration(CosmosDbConfiguration configuration);

        Task<ValidationResult> ValidateConfiguration();
    }

    internal abstract class CosmosThroughputClientBase11 : ICosmosThroughputClientInitializer
    {
        protected CosmosDbConfiguration CosmosDbConfiguration { get; private set; }

        public abstract Task<CosmosThroughput> Get();

        public abstract Task<CosmosThroughput> Set(int throughput);

        public void UseConfiguration(CosmosDbConfiguration configuration)
        {
            if (CosmosDbConfiguration != null)
                throw new InvalidOperationException("Only one configuration may be used.");

            CosmosDbConfiguration = configuration;
        }

        public abstract Task<ValidationResult> ValidateConfiguration();
    }

}
