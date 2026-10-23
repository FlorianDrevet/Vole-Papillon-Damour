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
    bool IsManuallyRequested,
    bool IsRare,
    bool IsHidden,
    DateTime UpdatedAt,
    string? WorkKey,
    string? Publisher,
    int? PublicationYear,
    IReadOnlyList<string> OtherIsbns);
