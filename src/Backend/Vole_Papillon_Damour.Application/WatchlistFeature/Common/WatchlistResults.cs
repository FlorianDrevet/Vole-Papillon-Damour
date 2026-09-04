using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Domain.WatchlistAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.WatchlistFeature.Common;

public sealed record AddedWatchlistItemResult(
    Guid Id,
    WatchlistItemScope Scope,
    string? WorkId,
    string? Isbn13,
    DateTimeOffset AddedAt);

public sealed record MyWatchlistResult(
    DateTimeOffset GeneratedAt,
    WatchlistAlertStatus AlertStatus,
    int BounceCount,
    IReadOnlyList<MyWatchlistItemResult> Items);

public sealed record MyWatchlistItemResult(
    Guid Id,
    WatchlistItemScope Scope,
    string? WorkId,
    string? Isbn13,
    PublicCatalogBookResult? Book,
    DateTimeOffset AddedAt,
    DateTimeOffset? LastAlertAt);
