using System.Collections.Concurrent;
using System.Threading.Channels;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Infrastructure.Services.Bibliographic;

public sealed class BookMetadataEnrichmentQueue : IBookMetadataEnrichmentQueue
{
    private readonly Channel<Isbn13> channel = Channel.CreateUnbounded<Isbn13>(
        new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false,
        });
    private readonly ConcurrentDictionary<Isbn13, byte> pending = new();

    public void Enqueue(Isbn13 isbn13)
    {
        if (!pending.TryAdd(isbn13, 0))
        {
            return;
        }

        if (!channel.Writer.TryWrite(isbn13))
        {
            pending.TryRemove(isbn13, out _);
        }
    }

    public async ValueTask<Isbn13> DequeueAsync(CancellationToken cancellationToken)
    {
        var isbn13 = await channel.Reader.ReadAsync(cancellationToken);
        pending.TryRemove(isbn13, out _);
        return isbn13;
    }

    public async ValueTask<IReadOnlyList<Isbn13>> DequeueBatchAsync(
        int max,
        CancellationToken cancellationToken)
    {
        if (max <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(max));
        }

        var batch = new List<Isbn13>(max);
        var included = new HashSet<Isbn13>();
        var first = await DequeueAsync(cancellationToken);
        batch.Add(first);
        included.Add(first);
        if (max == 1)
        {
            return batch;
        }

        using var batchWindow = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        batchWindow.CancelAfter(TimeSpan.FromMilliseconds(500));
        while (batch.Count < max)
        {
            try
            {
                var next = await channel.Reader.ReadAsync(batchWindow.Token);
                pending.TryRemove(next, out _);
                if (included.Add(next))
                {
                    batch.Add(next);
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                break;
            }
        }

        return batch;
    }
}
