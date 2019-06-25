using System.Net.Http;
using System.Threading.Tasks;
using Eshopworld.Tests.Core;
using Xunit;

public class ProbeControllerTests : BaseApiTests
{
    [Fact, IsLayer0]
    public async Task ProbeEndpointTest()
    {
        var response = await HttpClient.SendAsync(new HttpRequestMessage(HttpMethod.Get, "http://a/probe"));

        response.EnsureSuccessStatusCode();
        response.Dispose();
    }
}
