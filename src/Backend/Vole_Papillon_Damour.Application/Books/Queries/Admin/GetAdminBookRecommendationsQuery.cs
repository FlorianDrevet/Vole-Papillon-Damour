using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Domain.RecommendationAggregate;

namespace Vole_Papillon_Damour.Application.Books.Queries.Admin;

public sealed record GetAdminBookRecommendationsQuery(
    string? Search,
    BookRecommendationJobStatus? Status,
    int Page = 1,
    int PageSize = 50)
    : IRequest<ErrorOr<AdminBookRecommendationPageResult>>;

public sealed record AdminBookRecommendationPageResult(
    DateTimeOffset GeneratedAt,
    IReadOnlyList<AdminBookRecommendationItemResult> Items,
    int TotalCount,
    int Page,
    int PageSize);

public sealed record AdminBookRecommendationItemResult(
    string Isbn13,
    string? Title,
    string? Authors,
    BookRecommendationJobStatus Status,
    BookRecommendationJobStage Stage,
    int AttemptCount,
    DateTimeOffset? RequestedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    string? LastError);
