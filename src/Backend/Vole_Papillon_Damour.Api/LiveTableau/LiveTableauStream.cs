using System.Globalization;
using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;

namespace Vole_Papillon_Damour.Api.LiveTableau;

/// <summary>
/// The SSE items sent to one client: the initial snapshot, then every newer
/// snapshot, and a <c>ping</c> event after each heartbeat interval of silence.
/// </summary>
public static class LiveTableauStream
{
    public const string PingEventType = "ping";

    public static async IAsyncEnumerable<SseItem<string>> ReadAsync(
        LiveTableauSnapshot initial,
        ILiveTableauSubscription subscription,
        TimeSpan heartbeat,
        TimeSpan reconnection,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using (subscription)
        {
            yield return ToItem(initial, reconnection);
            var lastRevision = initial.Revision;

            while (!cancellationToken.IsCancellationRequested)
            {
                var next = await WaitForNextAsync(subscription, heartbeat, cancellationToken);
                if (next.IsClosed)
                {
                    yield break;
                }

                if (next.Snapshot is null)
                {
                    yield return new SseItem<string>(string.Empty, PingEventType);
                    continue;
                }

                // Published before this client read its initial state: already included.
                if (next.Snapshot.Revision <= lastRevision)
                {
                    continue;
                }

                lastRevision = next.Snapshot.Revision;
                yield return ToItem(next.Snapshot, null);
            }
        }
    }

    private static async Task<NextSnapshot> WaitForNextAsync(
        ILiveTableauSubscription subscription,
        TimeSpan heartbeat,
        CancellationToken cancellationToken)
    {
        using var heartbeatTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        heartbeatTimeout.CancelAfter(heartbeat);
        try
        {
            if (!await subscription.Reader.WaitToReadAsync(heartbeatTimeout.Token))
            {
                return new NextSnapshot(null, IsClosed: true);
            }

            return subscription.Reader.TryRead(out var snapshot)
                ? new NextSnapshot(snapshot, IsClosed: false)
                : new NextSnapshot(null, IsClosed: false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new NextSnapshot(null, IsClosed: false);
        }
        catch (OperationCanceledException)
        {
            return new NextSnapshot(null, IsClosed: true);
        }
    }

    private static SseItem<string> ToItem(LiveTableauSnapshot snapshot, TimeSpan? reconnection) =>
        new(snapshot.Json)
        {
            EventId = snapshot.Revision.ToString(CultureInfo.InvariantCulture),
            ReconnectionInterval = reconnection
        };

    private readonly record struct NextSnapshot(LiveTableauSnapshot? Snapshot, bool IsClosed);
}
