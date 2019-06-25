using System.Diagnostics.CodeAnalysis;
using System.Fabric;
using Autofac;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.ServiceFabric;
using Microsoft.ServiceFabric.Actors.Client;

namespace Bullfrog.Common.DependencyInjection
{
    [ExcludeFromCodeCoverage]
    public class ServiceFabricModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.Register<IActorProxyFactory>(_ => new ActorProxyFactory());
            builder.Register(x =>
            {
                var serviceContext = x.ResolveOptional<ServiceContext>();
                return FabricTelemetryInitializerExtension.CreateFabricTelemetryInitializer(serviceContext);
            }).As<ITelemetryInitializer>();
        }
    }
}
