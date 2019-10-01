using System.Collections.Generic;
using Autofac;
using Eshopworld.Core;
using Eshopworld.DevOps;
using Eshopworld.ServiceFabric.Telemetry;
using Eshopworld.Telemetry;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Configuration;

namespace Eshopworld.ServiceFabric.DependencyInjection
{
    /// <summary>
    /// Registers some key - devops + runtime - level services.
    /// </summary>
    public class CoreModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            var config = EswDevOpsSdk.BuildConfiguration();

            builder.RegisterInstance(config)
                   .As<IConfigurationRoot>()
                   .SingleInstance();

            builder.Register<IBigBrother>(c =>
            {
                var telemetryClient = c.Resolve<TelemetryClient>();
                var telemetrySettings = c.Resolve<TelemetrySettings>();
                var bb = new BigBrother(telemetryClient, telemetrySettings.InternalKey);

                var bigBrotherInitializers = c.Resolve<IEnumerable<IBigBrotherConfigurator>>();
                foreach (var initializer in bigBrotherInitializers)
                {
                    initializer.Initialize(bb);
                }

                return bb;
            })
            .SingleInstance();

            builder.RegisterInstance(LogicalCallTelemetryInitializer.Instance).As<ITelemetryInitializer>();
        }
    }
}
