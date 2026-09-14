namespace Vole_Papillon_Damour.Application.Books.Common;

public sealed record AdminCatalogueFlowStatsResult(
    DateTimeOffset GeneratedAt,
    DateTimeOffset? From,
    DateTimeOffset? To,
    AdminCatalogueFunnelResult Funnel,
    IReadOnlyList<AdminCatalogueGenreFlowResult> FlowRateByGenre,
    IReadOnlyList<AdminCatalogueTimeToSellBucketResult> TimeToSellDistribution);

public sealed record AdminCatalogueFunnelResult(
    int ScannedCount,
    int KeptCount,
    decimal? KeptRatePercent,
    int SoldCount,
    decimal? SoldRatePercent,
    int DormantCount,
    int DormantOverYearCount);

public sealed record AdminCatalogueGenreFlowResult(
    string Genre,
    int KeptQuantity,
    int SoldQuantity,
    decimal? FlowRatePercent);

public sealed record AdminCatalogueTimeToSellBucketResult(
    string Bucket,
    int Quantity,
    decimal? SharePercent);
