using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using Autofac.Integration.ServiceFabric;
using Bullfrog.Actors.Helpers;
using Bullfrog.Actors.ResourceScalers;
using Bullfrog.Common.DependencyInjection;
using Castle.Core.Internal;
using Eshopworld.Telemetry;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Configuration;

[assembly: InternalsVisibleTo(InternalsVisible.ToDynamicProxyGenAssembly2)]
[assembly: InternalsVisibleTo("Bullfrog.Tests")]

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
                builder.RegisterModule<ThroughputClientModule>();
                builder.RegisterType<ScaleSetMonitor>().AsSelf();
                builder.RegisterType<ScaleSetScaler>().AsSelf();
                builder.RegisterType<ResourceScalerFactory>().As<IResourceScalerFactory>();

                builder.Register(c =>
                {
                    var insKey = c.Resolve<IConfigurationRoot>()["BBInstrumentationKey"];
                    var configuration = new TelemetryConfiguration(insKey);
                    foreach (var initializer in c.Resolve<IEnumerable<ITelemetryInitializer>>())
                    {
                        configuration.TelemetryInitializers.Add(initializer);
                    }
                    return configuration;
                });
                builder.RegisterType<TelemetryClient>().SingleInstance();

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
