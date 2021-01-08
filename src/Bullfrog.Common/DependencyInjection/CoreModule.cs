using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Autofac;
using Eshopworld.Core;
using Eshopworld.DevOps;
using Eshopworld.Messaging;
using Eshopworld.Telemetry.Configuration;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Configuration;
using Microsoft.ServiceFabric.Actors.Client;
using TelemetrySettings = Eshopworld.Telemetry.Configuration.TelemetrySettings;

namespace Bullfrog.Common.DependencyInjection
{
    /// <summary>
    /// some key  - devops + runtime -  level services are registered here
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class CoreModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            var configuration = new ConfigurationBuilder()
                .UseDefaultConfigs()
                .AddKeyVaultSecrets(new Dictionary<string, string>
                {
                    { "cm--ai-telemetry--instrumentation", "Telemetry:InstrumentationKey" },
                    { "cm--ai-telemetry--internal", "Telemetry:InternalKey" }
                }).Build();

            var telemetrySettings = configuration.GetSection("Telemetry").Get<TelemetrySettings>();
            builder.RegisterInstance(telemetrySettings).SingleInstance();

            var serviceBusSettings = configuration.BindSection<ServiceBusSettings>();
            configuration.Bind(serviceBusSettings);
            builder.RegisterInstance(serviceBusSettings).SingleInstance();

            builder.RegisterInstance(EswDevOpsSdk.BuildConfiguration())
                .As<IConfigurationRoot>()
                .SingleInstance();

            builder.RegisterInstance(new Messenger(serviceBusSettings.ConnectionString, serviceBusSettings.SubscriptionId))
                .As<IPublishEvents>()
                .SingleInstance();

            builder.RegisterType<ActorProxyFactory>().As<IActorProxyFactory>().SingleInstance();
            builder.RegisterType<DateTimeProvider>().As<IDateTimeProvider>().SingleInstance();
            builder.RegisterInstance(LogicalCallTelemetryInitializer.Instance).As<ITelemetryInitializer>();
        }

        private class DateTimeProvider : IDateTimeProvider
        {
            public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
        }
    }
}
