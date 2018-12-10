using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using Autofac.Integration.ServiceFabric;
using Bullfrog.Common.DependencyInjection;
using Castle.Core.Internal;
using Eshopworld.Telemetry;

[assembly: InternalsVisibleTo(InternalsVisible.ToDynamicProxyGenAssembly2)]

namespace Bullfrog.Actor
{

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
                builder.RegisterModule(new CoreModule());
                //builder.RegisterModule(new AzureManagementFluentModule());

                builder.RegisterServiceFabricSupport();

                builder.RegisterActor<ScaleManager>();

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
