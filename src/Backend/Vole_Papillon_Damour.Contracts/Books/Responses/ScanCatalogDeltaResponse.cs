namespace Vole_Papillon_Damour.Contracts.Books.Responses;

public sealed record ScanCatalogDeltaResponse(
    DateTime GeneratedAt,
    string NextWatermark,
    IReadOnlyList<ScanCatalogBookResponse> Books,
    ScanAssociationSettingsResponse Settings,
    ScanNextBookFairResponse? NextFair);
