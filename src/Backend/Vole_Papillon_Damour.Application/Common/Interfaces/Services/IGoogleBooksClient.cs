using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Common.Interfaces.Services;

public interface IGoogleBooksClient
{
    Task<BookMetadataResult?> FindAsync(Isbn13 isbn13, CancellationToken cancellationToken);
}
