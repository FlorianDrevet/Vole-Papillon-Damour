using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Common.Interfaces.Services;

public interface IBookMetadataEnrichmentQueue
{
    void Enqueue(Isbn13 isbn13);

    ValueTask<Isbn13> DequeueAsync(CancellationToken cancellationToken);
}
