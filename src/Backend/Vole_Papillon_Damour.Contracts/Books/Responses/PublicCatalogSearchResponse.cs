namespace Vole_Papillon_Damour.Contracts.Books.Responses;

public sealed record PublicCatalogSearchResponse(
    DateTime GeneratedAt,
    IReadOnlyList<PublicCatalogBookResponse> Books,
    int? TotalCount,
    bool HasMore,
    int Page,
    int PageSize,
    IReadOnlyList<string> Genres,
    IReadOnlyList<PublicCatalogGenreFacetResponse>? GenreFacets = null);

public sealed record PublicCatalogGenreFacetResponse(string Name, int BookCount);
