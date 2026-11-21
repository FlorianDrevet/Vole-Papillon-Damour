using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Favorites.Common;

public sealed record FavoriteAddedResult(BookCode Isbn13, bool AlreadyPresent, int Total);

public sealed record MergeMyFavoritesEntry(string Isbn13, DateTime AddedAt);

public sealed record MergeMyFavoritesResult(int Added, int Skipped, int Total);
