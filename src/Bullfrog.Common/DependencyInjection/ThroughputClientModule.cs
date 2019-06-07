using Autofac;
using Bullfrog.Common.Cosmos;

namespace Bullfrog.Common.DependencyInjection
{
    /// <summary>
    /// Registers classes related to throughput clients
    /// </summary>
    public class ThroughputClientModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<DataPlaneCosmosThroughputClientFactory>().As<ICosmosThroughputClientFactory>();
        }
    }
}
