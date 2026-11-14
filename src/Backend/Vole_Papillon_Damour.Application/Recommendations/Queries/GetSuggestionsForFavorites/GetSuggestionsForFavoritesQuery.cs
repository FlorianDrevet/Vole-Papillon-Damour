using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.Recommendations.Queries.GetMyRecommendations;

namespace Vole_Papillon_Damour.Application.Recommendations.Queries.GetSuggestionsForFavorites;

public sealed record GetSuggestionsForFavoritesQuery(
    IReadOnlyList<string> Isbn13s,
    int MinimumHearts) : IRequest<ErrorOr<SuggestionsForFavoritesResult>>;

public sealed record FavoriteSeedProcessingState(string Isbn13, string Status, string Stage);

public sealed record SuggestionsForFavoritesResult(
    RecommendationStatus Status,
    IReadOnlyList<PersonalRecommendation> Items,
    IReadOnlyList<FavoriteSeedProcessingState> Seeds,
    int MinimumHearts);
