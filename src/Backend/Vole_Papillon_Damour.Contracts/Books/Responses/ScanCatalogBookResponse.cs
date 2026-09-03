namespace Vole_Papillon_Damour.Contracts.Books.Responses;

public sealed record ScanCatalogBookResponse(
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
