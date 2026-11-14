using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Domain.RecommendationAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Recommendations.Queries.GetMyRecommendations;

public sealed record GetMyRecommendationsQuery(
    string ExternalId,
    string? Email,
    string? FirstName,
    string? LastName,
    int Limit,
    int? MinimumHearts = null,
    IReadOnlyCollection<string>? CandidateIsbns = null) : IRequest<ErrorOr<MyRecommendationsResult>>;

public sealed record MyRecommendationsResult(
    RecommendationStatus Status,
    IReadOnlyList<PersonalRecommendation> Items,
    int MinimumHearts = 3);

public enum RecommendationStatus
{
    Enabled,
    Disabled,
    NoPurchases,
    NoFavorites
}

public sealed record PersonalRecommendation(
    PublicCatalogBookResult Book,
    NeighborReason Reason,
    string SeedTitle,
    int Hearts = 0,
    int OtherContributorCount = 0)
{
    public string ReasonTitle => SeedTitle;
}
