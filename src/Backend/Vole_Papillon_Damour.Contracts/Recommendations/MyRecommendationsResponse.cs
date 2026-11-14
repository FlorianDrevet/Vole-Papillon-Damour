namespace Vole_Papillon_Damour.Contracts.Recommendations;

public sealed record MyRecommendationsResponse(
    string Status,
    IReadOnlyList<MyRecommendationResponseItem> Items,
    int MinimumHearts = 3);

public sealed record MyRecommendationResponseItem(
    string Isbn13,
    string? Title,
    string? Authors,
    string? Publisher,
    int? PublicationYear,
    string? PhysicalFormat,
    string? Language,
    string? Genre,
    string? WorkId,
    string? CoverUrl,
    int QuantityAvailable,
    int QuantityAnnounced,
    DateTimeOffset? NextFairAt,
    DateTimeOffset? LastAvailableAt,
    DateTimeOffset FirstSeenAt,
    DateTimeOffset UpdatedAt,
    bool IsRare,
    string? CoverSource,
    string? RareBookSlug,
    string Reason,
    string SeedTitle,
    int Hearts = 0,
    int OtherContributorCount = 0);
