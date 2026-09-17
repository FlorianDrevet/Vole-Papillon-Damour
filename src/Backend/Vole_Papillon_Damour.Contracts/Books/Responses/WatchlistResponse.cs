using Vole_Papillon_Damour.Contracts.RareBooks.Responses;

namespace Vole_Papillon_Damour.Contracts.Books.Responses;

public sealed record AddedWatchlistItemResponse(
    Guid Id,
    string Scope,
    string? WorkId,
    string? Isbn13,
    Guid? RareBookId,
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
    Guid? RareBookId,
    string? Title,
    string? Authors,
    string? Publisher,
    int? PublicationYear,
    string? CoverUrl,
    PublicCatalogBookResponse? Book,
    PublicRareBookResponse? RareBook,
    DateTimeOffset AddedAt,
    DateTimeOffset? LastAlertAt);
