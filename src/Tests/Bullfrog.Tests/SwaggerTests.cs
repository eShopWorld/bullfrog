using System.Threading.Tasks;
using Bullfrog.Client;
using Eshopworld.Tests.Core;
using FluentAssertions;
using Newtonsoft.Json;
using Xunit;

public class SwaggerTests : BaseApiTests
{
    [Fact, IsLayer0]
    public async Task SwaggerIsGenerated()
    {
        var json = await HttpClient.GetStringAsync("http://test/swagger/v1/swagger.json");
        var obj = JsonConvert.DeserializeObject(json);
        obj.Should().NotBeNull();
    }

    /// <summary>
    /// Validates that no unintendent changes has been made to json
    /// </summary>
    /// <remarks>
    /// Please see the file Client.json.update.txt in the project Bullfrog.Client for more details
    /// how to fix this test.
    /// </remarks>
    [Fact, IsLayer0]
    public async Task SwaggerIsNotAccidentallyChanged()
    {
        var swagger = SwaggerDefinition.Get().Replace("\r\n", "\n");
        var json = await HttpClient.GetStringAsync("http://test/swagger/v1/swagger.json");
        json.Should().Be(swagger);
    }
}
