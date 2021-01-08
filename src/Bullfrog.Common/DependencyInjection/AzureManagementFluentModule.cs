using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using Bullfrog.Common.Helpers;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Authentication;
using Microsoft.Azure.Management.ResourceManager.Fluent.Core;
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Rest;

namespace Bullfrog.Common.DependencyInjection
{
    public class AzureManagementFluentModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.Register(c =>
            {
                var tokenProvider = new AzureServiceTokenProvider();
                var tokenProviderAdapter = new AzureServiceTokenProviderAdapter(tokenProvider);
                return new TokenCredentials(tokenProviderAdapter);
            });

            builder.Register(c =>
            {
                var tokenCredentials = c.Resolve<TokenCredentials>();
                var credentials = new AzureCredentials(tokenCredentials, tokenCredentials, string.Empty,
                        AzureEnvironment.AzureGlobalCloud);
                return Azure.Authenticate(credentials);
            });

            builder.Register(c =>
            {
                var serviceBusSettings = c.Resolve<ServiceBusSettings>();
                var authenticated = c.Resolve<Azure.IAuthenticated>();
                return authenticated.WithSubscription(serviceBusSettings.SubscriptionId);
            });

            builder.Register(c =>
            {
                var tokenCredentials = c.Resolve<TokenCredentials>();
                var azureCredentials = new AzureCredentials(
                    tokenCredentials, tokenCredentials, string.Empty, AzureEnvironment.AzureGlobalCloud);
                return RestClient.Configure()
                    .WithEnvironment(AzureEnvironment.AzureGlobalCloud)
                    .WithCredentials(azureCredentials)
                    .Build();
            });

            builder.Register(c =>
            {
                var restClient = c.Resolve<RestClient>();
                return new ResourceManagementClient(restClient);
            }).As<IResourceManagementClient>();

            builder.RegisterType<ScaleSetMonitor>();
        }

        private class AzureServiceTokenProviderAdapter : ITokenProvider
        {
            private const string Bearer = "Bearer";
            private readonly AzureServiceTokenProvider _azureTokenProvider;

            public AzureServiceTokenProviderAdapter(AzureServiceTokenProvider azureTokenProvider)
            {
                _azureTokenProvider = azureTokenProvider;
            }

            public async Task<AuthenticationHeaderValue> GetAuthenticationHeaderAsync(CancellationToken cancellationToken)
            {
                var token = await _azureTokenProvider.GetAccessTokenAsync("https://management.core.windows.net/", string.Empty);
                return new AuthenticationHeaderValue(Bearer, token);
            }
        }
    }
}
