using FluentAssertions;
using Vole_Papillon_Damour.Api.LiveTableau;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Api.tests.LiveTableau;

public sealed class LiveTableauHubTests
{
    [Fact]
    public void Publish_ReachesEveryClientOfTheEventAndOnlyThem()
    {
        var hub = new LiveTableauHub();
        var loto = AssoEventsId.CreateUnique();
        var otherEvent = AssoEventsId.CreateUnique();
        using var firstTab = hub.Subscribe(loto);
        using var secondTab = hub.Subscribe(loto);
        using var otherClient = hub.Subscribe(otherEvent);

        hub.Publish(loto, new LiveTableauSnapshot(1, "{}"));

        firstTab.Reader.TryRead(out var first).Should().BeTrue();
        secondTab.Reader.TryRead(out var second).Should().BeTrue();
        first!.Revision.Should().Be(1);
        second!.Revision.Should().Be(1);
        otherClient.Reader.TryRead(out _).Should().BeFalse();
    }

    [Fact]
    public void Publish_KeepsOnlyTheNewestSnapshotForASlowClient()
    {
        var hub = new LiveTableauHub();
        var loto = AssoEventsId.CreateUnique();
        using var slowClient = hub.Subscribe(loto);

        hub.Publish(loto, new LiveTableauSnapshot(1, "{\"n\":1}"));
        hub.Publish(loto, new LiveTableauSnapshot(2, "{\"n\":2}"));
        hub.Publish(loto, new LiveTableauSnapshot(3, "{\"n\":3}"));

        slowClient.Reader.TryRead(out var snapshot).Should().BeTrue();
        snapshot!.Revision.Should().Be(3);
        slowClient.Reader.TryRead(out _).Should().BeFalse();
    }

    [Fact]
    public void Dispose_RemovesTheClientAndForgetsTheRevisionOfAnUnwatchedEvent()
    {
        var hub = new LiveTableauHub();
        var loto = AssoEventsId.CreateUnique();
        var firstTab = hub.Subscribe(loto);
        var secondTab = hub.Subscribe(loto);
        hub.Publish(loto, new LiveTableauSnapshot(4, "{}"));

        firstTab.Dispose();
        hub.GetWatchedEvents().Should().ContainSingle().Which.Should().Be(loto);
        hub.GetKnownRevision(loto).Should().Be(4);

        secondTab.Dispose();
        secondTab.Dispose();
        hub.GetWatchedEvents().Should().BeEmpty();
        hub.GetKnownRevision(loto).Should().BeNull();
        secondTab.Reader.TryRead(out _).Should().BeTrue();
        secondTab.Reader.Completion.IsCompleted.Should().BeTrue();
    }

    [Fact]
    public void ObserveRevision_NeverGoesBack()
    {
        var hub = new LiveTableauHub();
        var loto = AssoEventsId.CreateUnique();
        using var client = hub.Subscribe(loto);

        hub.ObserveRevision(loto, 7);
        hub.ObserveRevision(loto, 5);

        hub.GetKnownRevision(loto).Should().Be(7);
    }
}
