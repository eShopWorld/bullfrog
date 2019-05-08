using System.Collections.Generic;
using Bullfrog.Actors.Models;
using Bullfrog.DomainEvents;
using Eshopworld.Tests.Core;
using FluentAssertions;
using Xunit;

public class RegisteredScaleEventTests
{
    [Theory, IsLayer0]
    [MemberData(nameof(StateData))]
    public void CurrentState(ScaleChangeType[] regionStates, ScaleChangeType currentState)
    {
        var scaleEvent = new RegisteredScaleEvent
        {
            Regions = new Dictionary<string, ScaleEventRegionState>()
        };
        var regionId = 0;
        foreach (var regionState in regionStates)
        {
            scaleEvent.Regions.Add($"r{regionId++}", new ScaleEventRegionState { State = regionState });
        }

        scaleEvent.CurrentState.Should().Be(currentState);
    }

    public static IEnumerable<object[]> StateData()
    {
        yield return new object[] { new ScaleChangeType[] { ScaleChangeType.Waiting }, ScaleChangeType.Waiting };
        yield return new object[] { new ScaleChangeType[] { ScaleChangeType.Waiting, ScaleChangeType.Waiting }, ScaleChangeType.Waiting };
        yield return new object[] { new ScaleChangeType[] { ScaleChangeType.ScaleInStarted, ScaleChangeType.Waiting }, ScaleChangeType.ScaleInStarted };
        yield return new object[] { new ScaleChangeType[] { ScaleChangeType.ScaleInComplete, ScaleChangeType.ScaleInComplete }, ScaleChangeType.ScaleInComplete };
        yield return new object[] { new ScaleChangeType[] { ScaleChangeType.ScaleInComplete, ScaleChangeType.ScaleInStarted }, ScaleChangeType.ScaleInStarted };
        yield return new object[] { new ScaleChangeType[] { ScaleChangeType.ScaleInComplete, ScaleChangeType.ScaleIssue }, ScaleChangeType.ScaleInStarted };
        yield return new object[] { new ScaleChangeType[] { ScaleChangeType.ScaleIssue, ScaleChangeType.ScaleOutStarted }, ScaleChangeType.ScaleIssue };
        yield return new object[] { new ScaleChangeType[] { ScaleChangeType.ScaleIssue, ScaleChangeType.ScaleOutComplete }, ScaleChangeType.ScaleIssue };
        yield return new object[] { new ScaleChangeType[] { ScaleChangeType.ScaleOutStarted, ScaleChangeType.Waiting }, ScaleChangeType.ScaleOutStarted };
        yield return new object[] { new ScaleChangeType[] { ScaleChangeType.ScaleOutStarted, ScaleChangeType.ScaleOutComplete }, ScaleChangeType.ScaleOutStarted };
        yield return new object[] { new ScaleChangeType[] { ScaleChangeType.ScaleOutComplete, ScaleChangeType.ScaleOutComplete }, ScaleChangeType.ScaleOutComplete };
    }
}
