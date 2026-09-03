using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Common.Interfaces.Services;

public interface IBibliographicMetadataResolver
{
    Task<BookMetadataResult?> ResolveAsync(Isbn13 isbn13, CancellationToken cancellationToken);
}
