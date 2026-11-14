using Vole_Papillon_Damour.Contracts.Recommendations;

namespace Vole_Papillon_Damour.Contracts.Favorites;

public sealed record MyFavoritesResponse(
    DateTimeOffset GeneratedAt,
    int Total,
    int Maximum,
    IReadOnlyList<MyFavoriteWorkResponse> Works);

public sealed record MyFavoriteWorkResponse(
    string WorkKey,
    string? WorkId,
    string Title,
    string? Authors,
    DateTimeOffset AddedAt,
    int EditionCount,
    bool IsComputingSuggestions,
    IReadOnlyList<MyFavoriteEditionResponse> Editions);

public sealed record MyFavoriteEditionResponse(
    string Isbn13,
    string Title,
    string? Authors,
    string? CoverUrl,
    DateTimeOffset AddedAt,
    int QuantityAvailable,
    int QuantityAnnounced,
    int FavoriteCount,
    string RecommendationStatus);

public sealed record FavoriteRequest(string Isbn13);

public sealed record MergeFavoritesRequest(IReadOnlyList<MergeFavoriteEntryRequest> Entries);

public sealed record MergeFavoriteEntryRequest(string Isbn13, DateTime AddedAt);

public sealed record MergeFavoritesResponse(int Added, int Skipped, int Total);

public sealed record FavoriteAddedResponse(string Isbn13, bool AlreadyPresent, int Total);

public sealed record AnonymousFavoriteResponse(string Isbn13, int Count);

public sealed record FavoriteSuggestionsRequest(IReadOnlyList<string> Isbn13s, int MinimumHearts);

public sealed record FavoriteSuggestionSeedResponse(string Isbn13, string Status, string Stage);

public sealed record FavoriteSuggestionsResponse(
    string Status,
    int MinimumHearts,
    IReadOnlyList<MyRecommendationResponseItem> Items,
    IReadOnlyList<FavoriteSuggestionSeedResponse> Seeds);
