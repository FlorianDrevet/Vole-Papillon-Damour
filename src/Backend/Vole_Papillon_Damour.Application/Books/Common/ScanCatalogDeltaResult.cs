namespace Vole_Papillon_Damour.Application.Books.Common;

public sealed record ScanCatalogDeltaResult(
    DateTime GeneratedAt,
    string NextWatermark,
    IReadOnlyList<ScanCatalogBookResult> Books,
    IReadOnlyList<ScanCatalogRareBookResult> RareBooks,
    IReadOnlyList<Guid> RemovedRareBookIds,
    AssociationSettingsResult Settings,
    ScanNextBookFairResult? NextFair);

public sealed record ScanCatalogRareBookResult(
    Guid Id,
    string? Isbn13,
    string Title,
    string? AuthorMention,
    decimal Price,
    string Condition,
    string? ShortDescription,
    Uri? Thumbnail,
    bool IsAvailable,
    DateTime UpdatedAt);
