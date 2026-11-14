namespace Vole_Papillon_Damour.Application.Favorites.Common;

public sealed record MyFavoritesResult(
    DateTimeOffset GeneratedAt,
    int Total,
    int Maximum,
    IReadOnlyList<MyFavoriteWorkResult> Works);

public sealed record MyFavoriteWorkResult(
    string WorkKey,
    string? WorkId,
    string Title,
    string? Authors,
    DateTimeOffset AddedAt,
    int EditionCount,
    bool IsComputingSuggestions,
    IReadOnlyList<MyFavoriteEditionResult> Editions);

public sealed record MyFavoriteEditionResult(
    string Isbn13,
    string Title,
    string? Authors,
    string? CoverUrl,
    DateTimeOffset AddedAt,
    int QuantityAvailable,
    int QuantityAnnounced,
    int FavoriteCount,
    string RecommendationStatus);
