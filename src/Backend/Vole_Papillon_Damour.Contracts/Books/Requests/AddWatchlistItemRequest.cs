namespace Vole_Papillon_Damour.Contracts.Books.Requests;

public sealed record AddWatchlistItemRequest(
    string Scope,
    string? WorkId,
    string? Isbn13);
