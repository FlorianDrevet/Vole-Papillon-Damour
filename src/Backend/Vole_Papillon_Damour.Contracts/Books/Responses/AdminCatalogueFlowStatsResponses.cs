namespace Vole_Papillon_Damour.Contracts.Books.Responses;

public sealed record AdminCatalogueFlowStatsResponse(
    DateTimeOffset GeneratedAt,
    DateTimeOffset? From,
    DateTimeOffset? To,
    AdminCatalogueFunnelResponse Funnel,
    IReadOnlyList<AdminCatalogueGenreFlowResponse> FlowRateByGenre,
    IReadOnlyList<AdminCatalogueTimeToSellBucketResponse> TimeToSellDistribution);

public sealed record AdminCatalogueFunnelResponse(
    int ScannedCount,
    int KeptCount,
    decimal? KeptRatePercent,
    int SoldCount,
    decimal? SoldRatePercent,
    int DormantCount,
    int DormantOverYearCount);

public sealed record AdminCatalogueGenreFlowResponse(
    string Genre,
    int KeptQuantity,
    int SoldQuantity,
    decimal? FlowRatePercent);

public sealed record AdminCatalogueTimeToSellBucketResponse(
    string Bucket,
    int Quantity,
    decimal? SharePercent);
