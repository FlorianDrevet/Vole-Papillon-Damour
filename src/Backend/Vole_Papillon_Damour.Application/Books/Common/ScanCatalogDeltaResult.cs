namespace Vole_Papillon_Damour.Application.Books.Common;

public sealed record ScanCatalogDeltaResult(
    DateTime GeneratedAt,
    DateTime NextWatermark,
    IReadOnlyList<ScanCatalogBookResult> Books,
    AssociationSettingsResult Settings);
