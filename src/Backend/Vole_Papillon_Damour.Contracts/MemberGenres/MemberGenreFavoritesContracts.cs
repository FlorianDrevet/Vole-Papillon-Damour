namespace Vole_Papillon_Damour.Contracts.MemberGenres;

public sealed record MemberGenreFavoritesResponse(IReadOnlyList<string> Genres);

public sealed record SetMemberGenreFavoriteRequest(string? Genre);
