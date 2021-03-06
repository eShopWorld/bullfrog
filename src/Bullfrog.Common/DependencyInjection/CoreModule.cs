﻿using System;
using System.Diagnostics.CodeAnalysis;
using Autofac;
using Bullfrog.Common.Telemetry;
using Eshopworld.Core;
using Eshopworld.DevOps;
using Eshopworld.Messaging;
using Eshopworld.Telemetry;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Configuration;
using Microsoft.ServiceFabric.Actors.Client;

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
            var config = EswDevOpsSdk.BuildConfiguration();

            builder.RegisterInstance(config)
                   .As<IConfigurationRoot>()
                   .SingleInstance();

            builder.Register<IBigBrother>(c =>
            {
                var configuration = c.Resolve<IConfigurationRoot>();

                var telemetryClient = c.Resolve<TelemetryClient>();
                var telemetrySettings = c.Resolve<TelemetrySettings>();
                var bb = new BigBrother(telemetryClient, telemetrySettings.InternalKey);

                var serviceBusConnectionString = configuration["SB:eda:ConnectionString"];
                var subscriptionId = configuration["Environment:SubscriptionId"];
                bb.PublishEventsToTopics(new Messenger(serviceBusConnectionString, subscriptionId));

                return bb;
            })
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
