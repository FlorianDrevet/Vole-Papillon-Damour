using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;

namespace Vole_Papillon_Damour.Infrastructure.Services.Bibliographic;

public sealed class BibliographicSearchService(IOpenLibraryClient openLibraryClient)
    : IBibliographicSearchService
{
    public Task<IReadOnlyList<BookReferenceSearchItem>> SearchAsync(
        string query,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        return openLibraryClient.SearchAsync(query, page, pageSize, cancellationToken);
    }
}
