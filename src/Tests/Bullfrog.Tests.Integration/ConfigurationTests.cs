using System;
using System.Net;
using System.Threading.Tasks;
using Client;
using Client.Models;
using Eshopworld.Tests.Core;
using FluentAssertions;
using Xunit;

[Collection(ApiClientCollection.TestFixtureName)]
public class ConfigurationTests
{
    private readonly ApiClientFixture _apiClientFixture;

    public ConfigurationTests(ApiClientFixture testFixture)
    {
        _apiClientFixture = testFixture;
    }

    [Fact, IsLayer3]
    public async Task ListOfScaleGroups()
    {
        var client = _apiClientFixture.GetAdminClient();

        var groups = await client.ListScaleGroupsAsync();

        groups.Should().NotBeNull();
    }

    [Fact, IsLayer3]
    public async Task GetDetailsOfExistingGroups()
    {
        var client = _apiClientFixture.GetAdminClient();

        var groups = await client.ListScaleGroupsAsync();

        foreach (var group in groups)
        {
            var scaleGroupDefinition = await client.GetDefinitionAsync(group);
            scaleGroupDefinition.Should().NotBeNull();
        }
    }

    [Fact, IsLayer3]
    public void EventsManagerCannotAccessConfiguration()
    {
        var client = _apiClientFixture.GetEventsManagerClient();

        Func<Task> func = () => client.ListScaleGroupsAsync();

        func.Should().Throw<ProblemDetailsException>()
            .Which.Response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }


    [Fact, IsLayer3]
    public void UnauthenticatedCannotAccessConfiguration()
    {
        var client = _apiClientFixture.GetBullfrogApiUnauthenticated();

        Func<Task> func = () => client.ListScaleGroupsAsync();

        func.Should().Throw<ProblemDetailsException>()
            .Which.Response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
