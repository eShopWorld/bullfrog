using Eshopworld.Messaging;
using Eshopworld.Telemetry;
using Microsoft.Extensions.Configuration;

namespace Eshopworld.ServiceFabric.DependencyInjection
{
    /// <summary>
    /// Configures BigBrother to send domain events to the specified topic.
    /// </summary>
    public class BigBrotherEventsPublishingConfigurator : IBigBrotherConfigurator
    {
        private readonly string _serviceBusConnectionString;
        private readonly string _subscriptionId;

        public BigBrotherEventsPublishingConfigurator(IConfigurationRoot configuration)
        {
            if (configuration is null)
                throw new System.ArgumentNullException(nameof(configuration));
            _serviceBusConnectionString = configuration["SB:eda:ConnectionString"];
            _subscriptionId = configuration["Environment:SubscriptionId"];
        }

        public BigBrotherEventsPublishingConfigurator(string serviceBusConnectionString, string subscriptionId)
        {
            _serviceBusConnectionString = serviceBusConnectionString;
            _subscriptionId = subscriptionId;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "BigBrother is not usually deleted, so the Messenger instance will not be either.")]
        public void Initialize(BigBrother bigBrother)
        {
            bigBrother.PublishEventsToTopics(new Messenger(_serviceBusConnectionString, _subscriptionId));
        }
    }
}
