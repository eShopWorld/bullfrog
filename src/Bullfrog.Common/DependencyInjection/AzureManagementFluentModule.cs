using System;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using Eshopworld.DevOps;
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
                // TODO: remove this workaround when SF is configured to provided this value
                if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AzureServicesAuthConnectionString")))
                {
                    var configurationRoot = c.Resolve<IConfigurationRoot>();
                    var authConnectionString = configurationRoot.GetSection("Bullfrog").GetSection("Auth")["ConnectionString"];
                    if (!string.IsNullOrWhiteSpace(authConnectionString))
                        Environment.SetEnvironmentVariable("AzureServicesAuthConnectionString", authConnectionString);
                }

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
                var subscriptionId = Environment.GetEnvironmentVariable("BullfrogAzureSubscriptionId")
                    ?? EswDevOpsSdk.GetSubscriptionId();
                var authenticated = c.Resolve<Azure.IAuthenticated>();
                return authenticated.WithSubscription(subscriptionId);
            });

            builder.RegisterType<CosmosDbHelper>().As<ICosmosDbHelper>();
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
