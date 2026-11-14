using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Favorites.Common;

public sealed record FavoriteReferenceResult(Isbn13 CanonicalIsbn13, bool Created);
