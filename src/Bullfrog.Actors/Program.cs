﻿using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using Autofac.Integration.ServiceFabric;
using Bullfrog.Actors.ResourceScalers;
using Bullfrog.Common.DependencyInjection;
using Eshopworld.DevOps;
using Eshopworld.ServiceFabric.DependencyInjection;
using Eshopworld.Telemetry;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DependencyCollector;
using Microsoft.ApplicationInsights.Extensibility;
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
                builder.RegisterModule<BullfrogCoreModule>();
                builder.RegisterModule<CoreModule>();
                builder.RegisterModule<AzureManagementFluentModule>();
                builder.RegisterModule<ServiceFabricModule>();
                builder.RegisterModule<ServiceFabricStatefulServicesModule>();
                builder.RegisterModule<ThroughputClientModule>();
                builder.RegisterType<ControlPlaneCosmosScaler>().AsSelf();
                builder.RegisterType<CosmosScaler>().AsSelf();
                builder.RegisterType<ScaleSetScaler>().AsSelf();
                builder.RegisterType<ResourceScalerFactory>().As<IResourceScalerFactory>();

                builder.RegisterType<OperationCorrelationTelemetryInitializer>().As<ITelemetryInitializer>();
                builder.RegisterType<HttpDependenciesParsingTelemetryInitializer>().As<ITelemetryInitializer>();
                builder.RegisterType<DependencyTrackingTelemetryModule>().As<ITelemetryModule>();

                builder.PublishDomainEventsToTopics();

                builder.Register(c =>
                {
                    var insKey = c.Resolve<IConfigurationRoot>()["BBInstrumentationKey"];
                    return new TelemetrySettings
                    {
                        InstrumentationKey = insKey,
                        InternalKey = insKey,
                    };
                });

                builder.Register(c =>
                {
                    var telemetrySettings = c.Resolve<TelemetrySettings>();
                    var configuration = new TelemetryConfiguration(telemetrySettings.InstrumentationKey);
                    foreach (var initializer in c.Resolve<IEnumerable<ITelemetryInitializer>>())
                    {
                        configuration.TelemetryInitializers.Add(initializer);
                    }

                    foreach (var modules in c.Resolve<IEnumerable<ITelemetryModule>>())
                    {
                        modules.Initialize(configuration);
                    }

                    return configuration;
                });
                builder.RegisterType<TelemetryClient>().SingleInstance().OnActivated(env =>
                    {
                        env.Instance.Context.GlobalProperties["AspNetCoreEnvironment"]
                            = Environment.GetEnvironmentVariable("AspNetCore_Environment");
                    });

                builder.RegisterServiceFabricSupport();

                builder.RegisterActor<ScaleManager>();
                builder.RegisterActor<ConfigurationManager>();
                builder.RegisterActor<ScaleEventStateReporter>();

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
