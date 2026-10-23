using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.Isbndb;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.IsbndbAggregate;

namespace Vole_Papillon_Damour.Application.Common.Interfaces.Services;

public interface IBibliographicMetadataResolver
{
    Task<BookMetadataResult?> ResolveAsync(
        Isbn13 isbn13,
        CancellationToken cancellationToken,
        bool includeBibliographicDetails = false,
        IsbndbUsage usage = IsbndbUsage.Scan,
        IsbndbLookup? prefetchedIsbndbLookup = null);

    Task<string?> ResolveWorkIdAsync(Isbn13 isbn13, CancellationToken cancellationToken);
}
