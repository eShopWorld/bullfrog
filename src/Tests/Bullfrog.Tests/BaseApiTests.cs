using System.Net.Http;
using Client;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Rest;

public class BaseApiTests
{
    public BaseApiTests()
    {
        var builder = new WebHostBuilder()
            .UseStartup<TestServerStartup>();
        var server = new TestServer(builder);
        HttpClient = server.CreateClient();
        ApiClient = new BullfrogApi(new TokenCredentials("aa"), HttpClient, false);
    }

    protected HttpClient HttpClient { get; }

    protected BullfrogApi ApiClient { get; }
}
