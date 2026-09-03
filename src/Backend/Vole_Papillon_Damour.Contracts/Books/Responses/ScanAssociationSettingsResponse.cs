namespace Vole_Papillon_Damour.Contracts.Books.Responses;

public sealed record ScanAssociationSettingsResponse(
    int DuplicateThreshold,
    int DemandSalesThreshold,
    int DeadStockMinAgeDays,
    int DeadStockMinQuantity,
    int WatchlistMaxItems,
    int AlertCooldownDays,
    int SessionIdleTimeoutMinutes,
    int AlertDelayMinutes,
    DateTime UpdatedAt);
