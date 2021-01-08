using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using Autofac.Integration.ServiceFabric;
using Bullfrog.Actors.ResourceScalers;
using Bullfrog.Common.DependencyInjection;
using Eshopworld.Telemetry;
using Eshopworld.Telemetry.Configuration;
using Microsoft.ApplicationInsights.DependencyCollector;
using Microsoft.ApplicationInsights.Extensibility;

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
                builder.AddStatefulServiceTelemetry();
                builder.RegisterModule<TelemetryModule>();
                builder.RegisterModule<CoreModule>();
                builder.RegisterModule<AzureManagementFluentModule>();
                builder.RegisterModule<ServiceFabricModule>();
                builder.RegisterModule<ThroughputClientModule>();
                builder.RegisterType<ControlPlaneCosmosScaler>().AsSelf();
                builder.RegisterType<CosmosScaler>().AsSelf();
                builder.RegisterType<ScaleSetScaler>().AsSelf();
                builder.RegisterType<ResourceScalerFactory>().As<IResourceScalerFactory>();

                builder.RegisterType<OperationCorrelationTelemetryInitializer>().As<ITelemetryInitializer>();
                builder.RegisterType<HttpDependenciesParsingTelemetryInitializer>().As<ITelemetryInitializer>();
                builder.RegisterType<DependencyTrackingTelemetryModule>().As<ITelemetryModule>();

                builder.RegisterServiceFabricSupport();

                builder.RegisterActor<ScaleManager>(typeof(MonitoredActorService));
                builder.RegisterActor<ConfigurationManager>(typeof(MonitoredActorService));
                builder.RegisterActor<ScaleEventStateReporter>(typeof(MonitoredActorService));

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
