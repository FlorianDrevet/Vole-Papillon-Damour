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
    int Limit) : IRequest<ErrorOr<MyRecommendationsResult>>;

public sealed record MyRecommendationsResult(
    RecommendationStatus Status,
    IReadOnlyList<PersonalRecommendation> Items);

public enum RecommendationStatus
{
    Enabled,
    Disabled,
    NoPurchases
}

public sealed record PersonalRecommendation(
    PublicCatalogBookResult Book,
    NeighborReason Reason,
    string SeedTitle);
