using System;
using System.Diagnostics.CodeAnalysis;
using Autofac;
using Eshopworld.DevOps;
using Eshopworld.Messaging;
using Eshopworld.Telemetry;
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

            builder.ConfigureBigBrother((bb, context) =>
            {
                var configuration = context.Resolve<IConfigurationRoot>();
                var serviceBusConnectionString = configuration["SB:eda:ConnectionString"];
                var subscriptionId = configuration["Environment:SubscriptionId"];
                bb.PublishEventsToTopics(new Messenger(serviceBusConnectionString, subscriptionId));
            });

            builder.RegisterType<ActorProxyFactory>().As<IActorProxyFactory>().SingleInstance();
            builder.RegisterType<DateTimeProvider>().As<IDateTimeProvider>().SingleInstance();
        }

        private class DateTimeProvider : IDateTimeProvider
        {
            public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
        }
    }
}
