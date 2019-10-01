using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Autofac;
using Eshopworld.DevOps;
using Microsoft.ApplicationInsights.DependencyCollector;
using Microsoft.ApplicationInsights.Extensibility;

namespace Eshopworld.ServiceFabric.DependencyInjection
{
    /// <summary>
    /// Registers telemetry components for stateful services. (ASP.Net Core stateless services should not use it).
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class ServiceFabricStatefulServicesModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<OperationCorrelationTelemetryInitializer>().As<ITelemetryInitializer>();
            builder.RegisterType<HttpDependenciesParsingTelemetryInitializer>().As<ITelemetryInitializer>();
            builder.RegisterType<DependencyTrackingTelemetryModule>().As<ITelemetryModule>();

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
        }
    }
}
