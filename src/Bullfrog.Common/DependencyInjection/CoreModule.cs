using System;
using Autofac;
using Eshopworld.Core;
using Eshopworld.DevOps;
using Eshopworld.Telemetry;
using Microsoft.Extensions.Configuration;
using Microsoft.ServiceFabric.Actors.Client;

namespace Bullfrog.Common.DependencyInjection
{
    /// <summary>
    /// some key  - devops + runtime -  level services are registered here
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
                var insKey = c.Resolve<IConfigurationRoot>()["BBInstrumentationKey"];
                return new BigBrother(insKey, insKey);
            })
            .SingleInstance();

            builder.RegisterType<ActorProxyFactory>().As<IActorProxyFactory>().SingleInstance();
            builder.RegisterType<DateTimeProvider>().As<IDateTimeProvider>().SingleInstance();
        }

        private class DateTimeProvider : IDateTimeProvider
        {
            public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
        }
    }
}
