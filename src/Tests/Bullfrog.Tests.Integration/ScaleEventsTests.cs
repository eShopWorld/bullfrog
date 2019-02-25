using System;
using System.Net;
using System.Threading.Tasks;
using Client;
using Client.Models;
using Eshopworld.Tests.Core;
using FluentAssertions;
using Xunit;

[Collection(ApiClientCollection.TestFixtureName)]
public class ScaleEventsTests
{
    private readonly ApiClientFixture _apiClientFixture;

    public ScaleEventsTests(ApiClientFixture testFixture)
    {
        _apiClientFixture = testFixture;
    }

    [Fact, IsLayer3]
    public async Task ListOfScheduledEvents()
    {
        var client = _apiClientFixture.GetAdminClient();
        var groups = await client.ListScaleGroupsAsync();
        if(groups.Count == 0)
            return;
        var eventsManagerClient = _apiClientFixture.GetEventsManagerClient();

        foreach(var group in groups)
        {
            var events = await eventsManagerClient.ListScheduledEventsAsync(group);
            events.Should().NotBeNull();
        }
    }

    [Fact, IsLayer3]
    public async Task UnauthenticatedCannotAccessEvents()
    {
        var client = _apiClientFixture.GetAdminClient();
        var groups = await client.ListScaleGroupsAsync();
        if (groups.Count == 0)
            return;
        var unauthenticatedClient = _apiClientFixture.GetBullfrogApiUnauthenticated();

        Func<Task> func = () => unauthenticatedClient.ListScheduledEventsAsync(groups[0]);

        func.Should().Throw<ProblemDetailsException>()
            .Which.Response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
