namespace Vole_Papillon_Damour.Contracts.Books.Responses;

public sealed record PublicCatalogWorkResponse(
    string WorkId,
    string? Title,
    string? Authors,
    IReadOnlyList<PublicCatalogBookResponse> Editions);
