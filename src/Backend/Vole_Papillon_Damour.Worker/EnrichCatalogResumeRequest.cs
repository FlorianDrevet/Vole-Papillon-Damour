namespace Vole_Papillon_Damour.Worker;

public sealed record EnrichCatalogResumeRequest
{
    public required DateTimeOffset FullCatalogRefreshStartedAt { get; init; }
    public required int LastCompletedBatchNumber { get; init; }
}
