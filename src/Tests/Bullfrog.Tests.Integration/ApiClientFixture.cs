using System;
using System.Collections.Generic;
using System.Net.Http;
using Client;
using Eshopworld.Core;
using Eshopworld.DevOps;
using EShopworld.Security.Services.Rest;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Rest;
using Moq;
using Xunit;

public class ApiClientFixture
{
    private readonly IConfigurationRoot _configuration;

    public ApiClientFixture()
    {
        _configuration = new ConfigurationBuilder()
            .UseDefaultConfigs()
            .AddKeyVaultSecrets(new Dictionary<string, string>
            {
                { "sts--sts-secret--bullfrog-api-admin-client", "Bullfrog:Testing:Clients:admin:ClientSecret" },
                { "sts--sts-secret--bullfrog-api-events-manager-client", "Bullfrog:Testing:Clients:eventsManager:ClientSecret" }
            })
            .Build();
    }

    public TokenCredentials GetAuthToken(string user)
    {
        var serviceProvider = new ServiceCollection().AddHttpClient().BuildServiceProvider();
        var httpClientFactory = serviceProvider.GetService<IHttpClientFactory>();

        var clientsConfig = _configuration.GetSection("Bullfrog:Testing:Clients");
        var clientConfig = clientsConfig.GetSection(user);
        var tokenProviderOptions = clientConfig.Get<RefreshingTokenProviderOptions>();
        var tokenProvider = new RefreshingTokenProvider(httpClientFactory, Mock.Of<IBigBrother>(), tokenProviderOptions);
        return new TokenCredentials(tokenProvider);
    }

    public IBullfrogApi GetAdminClient()
    {
        return GetBullfrogApi("admin");
    }

    public IBullfrogApi GetEventsManagerClient()
    {
        return GetBullfrogApi("eventsManager");
    }

    public IBullfrogApi GetBullfrogApi(string user)
    {
        var token = GetAuthToken(user);
        var url = new Uri(_configuration["Bullfrog:Testing:ApiUrl"]);
        return new BullfrogApi(url, token);
    }

    public IBullfrogApi GetBullfrogApiUnauthenticated()
    {
        var url = new Uri(_configuration["Bullfrog:Testing:ApiUrl"]);

        return new BullfrogApi(url, AnonymousCredential.Instance);
    }

    private class AnonymousCredential : ServiceClientCredentials
    {
        public static ServiceClientCredentials Instance { get; } = new AnonymousCredential();

        private AnonymousCredential()
        {
        }
    }
}

[CollectionDefinition(TestFixtureName, DisableParallelization = true)]
public class ApiClientCollection : ICollectionFixture<ApiClientFixture>
{
    public const string TestFixtureName = "ApiClient";
}
