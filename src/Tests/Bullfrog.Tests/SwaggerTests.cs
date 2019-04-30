using System.Threading.Tasks;
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
}
