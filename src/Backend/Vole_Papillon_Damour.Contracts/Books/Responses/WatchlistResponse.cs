namespace Vole_Papillon_Damour.Contracts.Books.Responses;

public sealed record AddedWatchlistItemResponse(
    Guid Id,
    string Scope,
    string? WorkId,
    string? Isbn13,
    DateTimeOffset AddedAt);

public sealed record WatchlistResponse(
    DateTimeOffset GeneratedAt,
    string AlertStatus,
    int BounceCount,
    IReadOnlyList<WatchlistItemResponse> Items);

public sealed record WatchlistItemResponse(
    Guid Id,
    string Scope,
    string? WorkId,
    string? Isbn13,
    PublicCatalogBookResponse? Book,
    DateTimeOffset AddedAt,
    DateTimeOffset? LastAlertAt);
