namespace Vole_Papillon_Damour.Application.Books.Common;

public sealed record AdminFairsEvolutionResult(
    DateTimeOffset GeneratedAt,
    DateTimeOffset? From,
    DateTimeOffset? To,
    int FairCount,
    int TotalSoldQuantity,
    decimal? TotalRevenue,
    decimal? AverageBasket,
    decimal? GrowthSinceFirstPercent,
    IReadOnlyList<AdminFairEvolutionSeasonResult> Seasons,
    IReadOnlyList<AdminFairEvolutionEntryResult> Fairs);

public sealed record AdminFairEvolutionSeasonResult(
    string Season,
    decimal? AverageSoldQuantity,
    int FairCount);

public sealed record AdminFairEvolutionEntryResult(
    Guid FairId,
    string Name,
    DateTimeOffset DateStart,
    DateTimeOffset? DateEnd,
    int SoldQuantity,
    decimal? Revenue,
    decimal? AverageBasket,
    decimal? VariationPercent,
    int? DaysOpen);
