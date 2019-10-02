using System;
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
        private const string ServiceBusConnectionStringConfigurationPath = "SB:eda:ConnectionString";
        private const string SubscriptionIdConfigurationPath = "Environment:SubscriptionId";
        private readonly string _serviceBusConnectionString;
        private readonly string _subscriptionId;

        public BigBrotherEventsPublishingConfigurator(IConfigurationRoot configuration)
        {
            if (configuration is null)
                throw new ArgumentNullException(nameof(configuration));
            _serviceBusConnectionString = configuration[ServiceBusConnectionStringConfigurationPath];
            _subscriptionId = configuration[SubscriptionIdConfigurationPath];

            if (_serviceBusConnectionString == null)
                throw new ArgumentException($"The configuration doesn't contain the EDA Service Bus connection string (path {ServiceBusConnectionStringConfigurationPath}).", nameof(configuration));

            if (_subscriptionId == null)
                throw new ArgumentException($"The configuration doesn't contain Subscription ID value (path {SubscriptionIdConfigurationPath}).", nameof(configuration));
        }

        public BigBrotherEventsPublishingConfigurator(string serviceBusConnectionString, string subscriptionId)
        {
            _serviceBusConnectionString = serviceBusConnectionString ?? throw new ArgumentNullException(nameof(serviceBusConnectionString));
            _subscriptionId = subscriptionId ?? throw new ArgumentNullException(nameof(subscriptionId));
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "BigBrother is not usually deleted, so the Messenger instance will not be either.")]
        public void Initialize(BigBrother bigBrother)
        {
            bigBrother.PublishEventsToTopics(new Messenger(_serviceBusConnectionString, _subscriptionId));
        }
    }
}
