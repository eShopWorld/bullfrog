using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using Autofac.Integration.ServiceFabric;
using Bullfrog.Actors.Helpers;
using Bullfrog.Common.DependencyInjection;
using Castle.Core.Internal;
using Eshopworld.Telemetry;
using Microsoft.ServiceFabric.Actors.Client;

[assembly: InternalsVisibleTo(InternalsVisible.ToDynamicProxyGenAssembly2)]

namespace Bullfrog.Actors
{
    [ExcludeFromCodeCoverage]
    internal static class Program
    {
        /// <summary>
        /// This is the entry point of the service host process.
        /// </summary>
        private static async Task Main()
        {
            try
            {
                var builder = new ContainerBuilder();
                builder.RegisterModule<CoreModule>();
                builder.RegisterModule<AzureManagementFluentModule>();
                builder.RegisterModule<ServiceFabricModule>();
                builder.RegisterType<ScaleSetManager>().As<IScaleSetManager>();
                builder.RegisterType<CosmosManager>().As<ICosmosManager>();

                builder.RegisterServiceFabricSupport();

                builder.RegisterActor<ScaleManager>();
                builder.RegisterActor<ConfigurationManager>();

                using (var container = builder.Build())
                {
                    await Task.Delay(Timeout.Infinite);
                }
            }
            catch (Exception e)
            {
                ActorEventSource.Current.ActorHostInitializationFailed(e.ToString());
                BigBrother.Write(e);
                throw;
            }
        }
    }
}
