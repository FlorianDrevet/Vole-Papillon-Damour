namespace Vole_Papillon_Damour.Contracts.Books.Responses;

public sealed record ScanCatalogDeltaResponse(
    DateTime GeneratedAt,
    DateTime NextWatermark,
    IReadOnlyList<ScanCatalogBookResponse> Books,
    ScanAssociationSettingsResponse Settings);
