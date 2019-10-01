using Autofac;
using Microsoft.Extensions.Configuration;

namespace Eshopworld.ServiceFabric.DependencyInjection
{
    public static class ContainerBuilderExtensions
    {
        /// <summary>
        /// Configures BigBrother to send domain events to a Service Bus defined in a configuration.
        /// </summary>
        /// <param name="containerBuilder">The container builder.</param>
        /// <returns>The updated container builder.</returns>
        public static ContainerBuilder PublishDomainEventsToTopics(this ContainerBuilder containerBuilder)
        {
            containerBuilder.Register(c =>
            {
                var configuration = c.Resolve<IConfigurationRoot>();
                return new BigBrotherEventsPublishingConfigurator(configuration);
            }).As<IBigBrotherConfigurator>();

            return containerBuilder;
        }
    }
}
