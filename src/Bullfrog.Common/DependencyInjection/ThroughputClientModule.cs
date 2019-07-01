using System.Diagnostics.CodeAnalysis;
using Autofac;
using Bullfrog.Common.Cosmos;
using Bullfrog.Common.Models;

namespace Bullfrog.Common.DependencyInjection
{
    /// <summary>
    /// Registers classes related to throughput clients
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class ThroughputClientModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<DataPlaneCosmosThroughputClient>().As<ICosmosThroughputClient>();

            builder.RegisterType<CosmosDataPlaneAccessValidator>()
                .As<ICosmosAccessValidator<CosmosDbDataPlaneConnection>>();
            builder.RegisterType<CosmosControlPlaneAccessValidator>()
                .As<ICosmosAccessValidator<CosmosDbControlPlaneConnection>>();
        }
    }
}
