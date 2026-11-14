using System.Collections.Concurrent;
using System.Threading.Channels;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.IsbndbAggregate;

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
    private readonly ConcurrentDictionary<Isbn13, BookMetadataEnrichmentRequest> pending = new();

    public void Enqueue(Isbn13 isbn13, IsbndbUsage usage = IsbndbUsage.Scan, bool forceFallback = false)
    {
        var request = new BookMetadataEnrichmentRequest(isbn13, usage, forceFallback);
        if (!pending.TryAdd(isbn13, request))
        {
            pending.AddOrUpdate(
                isbn13,
                request,
                (_, existing) => new BookMetadataEnrichmentRequest(
                    isbn13,
                    usage == IsbndbUsage.Favorites ? usage : existing.Usage,
                    forceFallback || existing.ForceFallback));
            return;
        }

        if (!channel.Writer.TryWrite(isbn13))
        {
            pending.TryRemove(isbn13, out _);
        }
    }

    public async ValueTask<BookMetadataEnrichmentRequest> DequeueAsync(CancellationToken cancellationToken)
    {
        var isbn13 = await channel.Reader.ReadAsync(cancellationToken);
        return pending.TryRemove(isbn13, out var request)
            ? request
            : new BookMetadataEnrichmentRequest(isbn13, IsbndbUsage.Scan, ForceFallback: false);
    }

    public async ValueTask<IReadOnlyList<BookMetadataEnrichmentRequest>> DequeueBatchAsync(
        int max,
        CancellationToken cancellationToken)
    {
        if (max <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(max));
        }

        var batch = new List<BookMetadataEnrichmentRequest>(max);
        var included = new HashSet<Isbn13>();
        var first = await DequeueAsync(cancellationToken);
        batch.Add(first);
        included.Add(first.Isbn13);
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
                var isbn13 = await channel.Reader.ReadAsync(batchWindow.Token);
                var next = pending.TryRemove(isbn13, out var request)
                    ? request
                    : new BookMetadataEnrichmentRequest(isbn13, IsbndbUsage.Scan, ForceFallback: false);
                if (included.Add(next.Isbn13))
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
