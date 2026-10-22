namespace Vole_Papillon_Damour.Worker;

public sealed record EnrichCatalogBatchRequest(
    int BatchNumber,
    int BatchSize,
    DateTime FullCatalogRefreshStartedAt);
