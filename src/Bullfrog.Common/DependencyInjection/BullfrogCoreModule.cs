using System;
using System.Diagnostics.CodeAnalysis;
using Autofac;
using Microsoft.ServiceFabric.Actors.Client;

namespace Bullfrog.Common.DependencyInjection
{
    /// <summary>
    /// Registers basic Bullfrog components.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class BullfrogCoreModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<ActorProxyFactory>().As<IActorProxyFactory>().SingleInstance();
            builder.RegisterType<DateTimeProvider>().As<IDateTimeProvider>().SingleInstance();
        }

        private class DateTimeProvider : IDateTimeProvider
        {
            public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
        }
    }
}
