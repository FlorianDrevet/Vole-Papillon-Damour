namespace Vole_Papillon_Damour.Contracts.Books.Responses;

public sealed record PublicCatalogSearchResponse(
    DateTime GeneratedAt,
    IReadOnlyList<PublicCatalogBookResponse> Books,
    int TotalCount,
    int Page,
    int PageSize,
    IReadOnlyList<string> Genres);
