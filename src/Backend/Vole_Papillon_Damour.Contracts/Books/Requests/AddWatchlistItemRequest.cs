namespace Vole_Papillon_Damour.Contracts.Books.Requests;

public sealed record AddWatchlistItemRequest(
    string Scope,
    string? WorkId,
    string? Isbn13,
    string? Title = null,
    string? Authors = null,
    string? Publisher = null,
    int? PublicationYear = null,
    string? CoverUrl = null);
