using System.Threading.Channels;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Api.LiveTableau;

/// <summary>
/// In-memory fan-out of live tableau snapshots to the SSE clients connected to
/// this replica. Changes made on another replica arrive through
/// <see cref="LiveTableauSyncService"/>.
/// </summary>
public interface ILiveTableauHub
{
    /// <summary>
    /// Registers a client. Only the newest snapshot is kept for a slow client:
    /// each snapshot is a complete state, older ones are useless.
    /// </summary>
    ILiveTableauSubscription Subscribe(AssoEventsId assoEventsId);

    /// <summary>Sends <paramref name="snapshot"/> to every client of the event and records its revision.</summary>
    void Publish(AssoEventsId assoEventsId, LiveTableauSnapshot snapshot);

    /// <summary>Records a revision this replica already knows, without sending anything.</summary>
    void ObserveRevision(AssoEventsId assoEventsId, int revision);

    /// <summary>Highest revision published or observed on this replica, if any.</summary>
    int? GetKnownRevision(AssoEventsId assoEventsId);

    /// <summary>Events with at least one connected client.</summary>
    IReadOnlyCollection<AssoEventsId> GetWatchedEvents();
}

public interface ILiveTableauSubscription : IDisposable
{
    ChannelReader<LiveTableauSnapshot> Reader { get; }
}
