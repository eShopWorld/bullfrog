using System.Threading.Tasks;
using Client;
using Eshopworld.Tests.Core;
using Xunit;

[Collection(ApiClientCollection.TestFixtureName)]
public class ProbeTests
{
    private readonly ApiClientFixture _apiClientFixture;

    public ProbeTests(ApiClientFixture testFixture)
    {
        _apiClientFixture = testFixture;
    }

    [Fact, IsLayer3]
    public async Task ProbeGetTest()
    {
        var client = _apiClientFixture.GetBullfrogApiUnauthenticated();

        await client.GetAsync();

        client.Dispose();
    }
}
