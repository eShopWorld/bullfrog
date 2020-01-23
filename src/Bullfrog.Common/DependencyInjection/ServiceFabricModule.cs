using System.Diagnostics.CodeAnalysis;
using Autofac;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.ServiceFabric.Module;
using Microsoft.ServiceFabric.Actors.Client;

namespace Bullfrog.Common.DependencyInjection
{
    [ExcludeFromCodeCoverage]
    public class ServiceFabricModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.Register<IActorProxyFactory>(_ => new ActorProxyFactory());

            // TODO: Should ServiceRemotingDependencyTrackingTelemetryModule be moved to Eshopworld.Telemetry.Configuration.ServiceFabricTelemetryModule?
            // is it applicable only to statefull services?
            builder.RegisterType<ServiceRemotingDependencyTrackingTelemetryModule>().As<ITelemetryModule>();
        }
    }
}
