namespace Vole_Papillon_Damour.Contracts.Books.Responses;

public sealed record AdminFairsEvolutionResponse(
    DateTimeOffset GeneratedAt,
    DateTimeOffset? From,
    DateTimeOffset? To,
    int FairCount,
    int TotalSoldQuantity,
    decimal? TotalRevenue,
    decimal? AverageBasket,
    decimal? GrowthSinceFirstPercent,
    IReadOnlyList<AdminFairEvolutionSeasonResponse> Seasons,
    IReadOnlyList<AdminFairEvolutionEntryResponse> Fairs);

public sealed record AdminFairEvolutionSeasonResponse(
    string Season,
    decimal? AverageSoldQuantity,
    int FairCount);

public sealed record AdminFairEvolutionEntryResponse(
    Guid FairId,
    string Name,
    DateTimeOffset DateStart,
    DateTimeOffset? DateEnd,
    int SoldQuantity,
    decimal? Revenue,
    decimal? AverageBasket,
    decimal? VariationPercent,
    int? DaysOpen);
