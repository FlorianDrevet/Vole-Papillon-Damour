using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.IsbndbAggregate;

namespace Vole_Papillon_Damour.Application.Common.Interfaces.Services;

public interface IBookMetadataEnrichmentQueue
{
    void Enqueue(Isbn13 isbn13, IsbndbUsage usage = IsbndbUsage.Scan, bool forceFallback = false);

    ValueTask<BookMetadataEnrichmentRequest> DequeueAsync(CancellationToken cancellationToken);

    ValueTask<IReadOnlyList<BookMetadataEnrichmentRequest>> DequeueBatchAsync(
        int max,
        CancellationToken cancellationToken);
}
