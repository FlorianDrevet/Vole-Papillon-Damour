namespace Vole_Papillon_Damour.Application.Books.Common;

public sealed record ScanCatalogBookResult(
    string Isbn13,
    string? Title,
    string? Authors,
    string? WorkId,
    int QtyAvailable,
    int QtyAnnounced,
    int SalesCount,
    bool IsWanted,
    bool IsRare,
    bool IsHidden,
    DateTime UpdatedAt);
