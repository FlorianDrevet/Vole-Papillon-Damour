using Vole_Papillon_Damour.Application.Books.Common;

namespace Vole_Papillon_Damour.Application.Common.Interfaces.Services;

public interface IBnfSruSearchClient
{
    Task<IReadOnlyList<BookReferenceSearchItem>> SearchAsync(
        string query,
        int page,
        int pageSize,
        CancellationToken cancellationToken);
}
