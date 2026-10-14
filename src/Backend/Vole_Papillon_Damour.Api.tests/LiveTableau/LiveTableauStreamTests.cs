using System.Net.ServerSentEvents;
using FluentAssertions;
using Vole_Papillon_Damour.Api.LiveTableau;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Api.tests.LiveTableau;

public sealed class LiveTableauStreamTests
{
    private static readonly TimeSpan LongHeartbeat = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan Reconnection = TimeSpan.FromSeconds(3);

    [Fact]
    public async Task ReadAsync_StartsWithTheInitialStateItsRevisionAndTheReconnectionDelay()
    {
        var hub = new LiveTableauHub();
        var subscription = hub.Subscribe(AssoEventsId.CreateUnique());
        using var cancellation = new CancellationTokenSource();
        await using var stream = LiveTableauStream
            .ReadAsync(new LiveTableauSnapshot(3, "{\"state\":3}"), subscription, LongHeartbeat, Reconnection, cancellation.Token)
            .GetAsyncEnumerator();

        (await stream.MoveNextAsync()).Should().BeTrue();

        stream.Current.Data.Should().Be("{\"state\":3}");
        stream.Current.EventId.Should().Be("3");
        stream.Current.ReconnectionInterval.Should().Be(Reconnection);
        stream.Current.EventType.Should().Be("message");
    }

    [Fact]
    public async Task ReadAsync_SendsNewerSnapshotsAndSkipsOnesAlreadyInTheInitialState()
    {
        var hub = new LiveTableauHub();
        var loto = AssoEventsId.CreateUnique();
        var subscription = hub.Subscribe(loto);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var stream = LiveTableauStream
            .ReadAsync(new LiveTableauSnapshot(5, "{}"), subscription, LongHeartbeat, Reconnection, cancellation.Token)
            .GetAsyncEnumerator();
        await stream.MoveNextAsync();

        hub.Publish(loto, new LiveTableauSnapshot(5, "{\"stale\":true}"));
        var next = stream.MoveNextAsync();
        hub.Publish(loto, new LiveTableauSnapshot(6, "{\"state\":6}"));

        (await next).Should().BeTrue();
        stream.Current.EventId.Should().Be("6");
        stream.Current.Data.Should().Be("{\"state\":6}");
        stream.Current.ReconnectionInterval.Should().BeNull();
    }

    [Fact]
    public async Task ReadAsync_SendsAPingAfterAHeartbeatOfSilence()
    {
        var hub = new LiveTableauHub();
        var subscription = hub.Subscribe(AssoEventsId.CreateUnique());
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var stream = LiveTableauStream
            .ReadAsync(new LiveTableauSnapshot(1, "{}"), subscription, TimeSpan.FromMilliseconds(50), Reconnection, cancellation.Token)
            .GetAsyncEnumerator();
        await stream.MoveNextAsync();

        (await stream.MoveNextAsync()).Should().BeTrue();

        stream.Current.EventType.Should().Be(LiveTableauStream.PingEventType);
        stream.Current.EventId.Should().BeNull();
    }

    [Fact]
    public async Task ReadAsync_EndsAndUnsubscribesWhenTheClientLeaves()
    {
        var hub = new LiveTableauHub();
        var loto = AssoEventsId.CreateUnique();
        var subscription = hub.Subscribe(loto);
        using var cancellation = new CancellationTokenSource();
        var items = new List<SseItem<string>>();
        var reading = Task.Run(async () =>
        {
            await foreach (var item in LiveTableauStream.ReadAsync(
                               new LiveTableauSnapshot(1, "{}"), subscription, LongHeartbeat, Reconnection, cancellation.Token))
            {
                items.Add(item);
            }
        });
        await Task.Delay(100);
        cancellation.Cancel();
        await reading.WaitAsync(TimeSpan.FromSeconds(5));

        items.Should().ContainSingle();
        hub.GetWatchedEvents().Should().BeEmpty();
    }
}
