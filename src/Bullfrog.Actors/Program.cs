using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using Autofac.Integration.ServiceFabric;
using Bullfrog.Actors.Helpers;
using Bullfrog.Actors.ResourceScalers;
using Bullfrog.Common.DependencyInjection;
using Eshopworld.Telemetry;
using Eshopworld.Telemetry.Configuration;
using Microsoft.Extensions.Configuration;

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
                builder.RegisterType<ControlPlaneCosmosScaler>();
                builder.RegisterType<CosmosScaler>();
                builder.RegisterType<ScaleSetScaler>();
                builder.RegisterType<ResourceScalerFactory>().As<IResourceScalerFactory>();
                builder.RegisterType<RunbookVmssScaler>();
                builder.RegisterType<RunbookClient>().As<IRunbookClient>();
                builder.RegisterType<AutoscaleSettingsHandlerFactory>().As<IAutoscaleSettingsHandlerFactory>();

                builder.AddStatefullServiceTelemetry();
                builder.RegisterModule<TelemetryModule>();
                builder.RegisterModule<ServiceFabricTelemetryModule>();
                builder.Register(c =>
                {
                    var insKey = c.Resolve<IConfigurationRoot>()["BBInstrumentationKey"];
                    return new TelemetrySettings
                    {
                        InstrumentationKey = insKey,
                        InternalKey = insKey,
                    };
                });

                builder.RegisterActor<ScaleManager>();
                builder.RegisterActor<ConfigurationManager>();
                builder.RegisterActor<ScaleEventStateReporter>();
                builder.RegisterActor<ResourceScalingActor>();

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
