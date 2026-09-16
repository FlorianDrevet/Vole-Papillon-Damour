namespace Vole_Papillon_Damour.Contracts.Books.Responses;

public sealed record ScanCatalogDeltaResponse(
    DateTime GeneratedAt,
    string NextWatermark,
    IReadOnlyList<ScanCatalogBookResponse> Books,
    IReadOnlyList<ScanCatalogRareBookResponse> RareBooks,
    ScanAssociationSettingsResponse Settings,
    ScanNextBookFairResponse? NextFair);

public sealed record ScanCatalogRareBookResponse(
    Guid Id,
    string Isbn13,
    string Title,
    string? AuthorMention,
    decimal Price,
    string Shelf,
    string Condition,
    string? ShortDescription,
    Uri? Thumbnail,
    bool IsAvailable,
    DateTime UpdatedAt);
