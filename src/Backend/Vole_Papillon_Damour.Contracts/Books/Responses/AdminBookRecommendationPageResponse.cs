namespace Vole_Papillon_Damour.Contracts.Books.Responses;

public sealed record AdminBookRecommendationPageResponse(
    DateTimeOffset GeneratedAt,
    IReadOnlyList<AdminBookRecommendationResponse> Items,
    int TotalCount,
    int Page,
    int PageSize);

public sealed record AdminBookRecommendationResponse(
    string Isbn13,
    string? Title,
    string? Authors,
    string Status,
    string Stage,
    int AttemptCount,
    DateTimeOffset? RequestedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    string? LastError);

public sealed record AdminBookRecommendationRetryResponse(
    string Isbn13,
    string Status,
    int RequestVersion);

public sealed record AdminBookRecommendationBackfillResponse(
    Guid BackfillId,
    string Status);
