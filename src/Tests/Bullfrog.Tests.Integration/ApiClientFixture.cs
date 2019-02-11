using System;
using Client;
using Eshopworld.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Rest;
using Moq;
using Security.Services.Rest;
using Xunit;

public class ApiClientFixture
{
    private readonly IConfigurationRoot configuration;

    public ApiClientFixture()
    {
        configuration = Eshopworld.DevOps.EswDevOpsSdk.BuildConfiguration(useTest: true);
    }

    public TokenCredentials GetAuthToken(string user)
    {
        var clientsConfig = configuration.GetSection("Bullfrog:Testing:Clients");
        var clientConfig = clientsConfig.GetSection(user);
        var tokenProviderOptions = clientConfig.Get<RefreshingTokenProviderOptions>();
        var tokenProvider = new RefreshingTokenProvider(new TokenClientFactory(), Mock.Of<IBigBrother>(), tokenProviderOptions);
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
        var url = new Uri(configuration["Bullfrog:Testing:ApiUrl"]);
        return new BullfrogApi(url, token);
    }

    public IBullfrogApi GetBullfrogApiUnauthenticated()
    {
        var url = new Uri(configuration["Bullfrog:Testing:ApiUrl"]);

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
