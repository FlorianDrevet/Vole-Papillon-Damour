using System.Collections.Concurrent;
using System.Threading.Channels;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Api.LiveTableau;

public sealed class LiveTableauHub : ILiveTableauHub
{
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, Channel<LiveTableauSnapshot>>> _subscribers = new();
    private readonly ConcurrentDictionary<Guid, int> _knownRevisions = new();

    public ILiveTableauSubscription Subscribe(AssoEventsId assoEventsId)
    {
        var channel = Channel.CreateBounded<LiveTableauSnapshot>(new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });
        var subscriptionId = Guid.NewGuid();
        var eventSubscribers = _subscribers.GetOrAdd(assoEventsId.Value, _ => new());
        eventSubscribers[subscriptionId] = channel;

        return new Subscription(channel, () => Unsubscribe(assoEventsId.Value, subscriptionId));
    }

    public void Publish(AssoEventsId assoEventsId, LiveTableauSnapshot snapshot)
    {
        ObserveRevision(assoEventsId, snapshot.Revision);
        if (!_subscribers.TryGetValue(assoEventsId.Value, out var eventSubscribers))
        {
            return;
        }

        foreach (var channel in eventSubscribers.Values)
        {
            channel.Writer.TryWrite(snapshot);
        }
    }

    public void ObserveRevision(AssoEventsId assoEventsId, int revision)
    {
        _knownRevisions.AddOrUpdate(
            assoEventsId.Value,
            revision,
            (_, known) => Math.Max(known, revision));
    }

    public int? GetKnownRevision(AssoEventsId assoEventsId) =>
        _knownRevisions.TryGetValue(assoEventsId.Value, out var revision) ? revision : null;

    public IReadOnlyCollection<AssoEventsId> GetWatchedEvents() =>
        _subscribers
            .Where(entry => !entry.Value.IsEmpty)
            .Select(entry => new AssoEventsId(entry.Key))
            .ToList();

    private void Unsubscribe(Guid eventId, Guid subscriptionId)
    {
        if (!_subscribers.TryGetValue(eventId, out var eventSubscribers) ||
            !eventSubscribers.TryRemove(subscriptionId, out var channel))
        {
            return;
        }

        channel.Writer.TryComplete();
        if (eventSubscribers.IsEmpty)
        {
            // Nobody watches this event any more: forget its revision too, so the
            // next first client triggers a fresh comparison with the database.
            _subscribers.TryRemove(KeyValuePair.Create(eventId, eventSubscribers));
            _knownRevisions.TryRemove(eventId, out _);
        }
    }

    private sealed class Subscription(Channel<LiveTableauSnapshot> channel, Action unsubscribe)
        : ILiveTableauSubscription
    {
        private int _disposed;

        public ChannelReader<LiveTableauSnapshot> Reader => channel.Reader;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                unsubscribe();
            }
        }
    }
}
