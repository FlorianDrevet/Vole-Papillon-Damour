using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.WatchlistAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.ManualRequests.Common;

internal static class ManualRequestWatchers
{
    public static Task<int> CountAsync(
        IProjectDbContext db,
        Isbn13 isbn13,
        string? workId,
        CancellationToken ct)
    {
        return db.Watchlists
            .AsNoTracking()
            .Where(watchlist => watchlist.AlertStatus == WatchlistAlertStatus.Active)
            .CountAsync(watchlist => db.WatchlistItems.Any(item =>
                item.UserId == watchlist.Id &&
                ((item.Scope == WatchlistItemScope.Edition && item.Isbn13 == isbn13) ||
                 (workId != null && item.Scope == WatchlistItemScope.Work && item.WorkId == workId))), ct);
    }

    /// <summary>
    /// Batch variant of <see cref="CountAsync"/>: one query for all keys, then the same rule per key
    /// (an active watchlist counts once when one of its items matches the edition or the work).
    /// Work ids are compared case-insensitively, like the database collation used by <see cref="CountAsync"/>.
    /// </summary>
    public static async Task<IReadOnlyDictionary<Guid, int>> CountManyAsync(
        IProjectDbContext db,
        IReadOnlyCollection<(Guid RequestId, Isbn13 Isbn13, string? WorkId)> keys,
        CancellationToken ct)
    {
        var items = keys.ToArray();
        var counts = new Dictionary<Guid, int>(items.Length);
        if (items.Length == 0)
        {
            return counts;
        }

        BookCode?[] isbns = items.Select(key => (BookCode?)key.Isbn13).Distinct().ToArray();
        var workIds = items
            .Where(key => key.WorkId is not null)
            .Select(key => key.WorkId!)
            .Distinct()
            .ToArray();
        var matches = await (
                from item in db.WatchlistItems.AsNoTracking()
                join watchlist in db.Watchlists.AsNoTracking() on item.UserId equals watchlist.Id
                where watchlist.AlertStatus == WatchlistAlertStatus.Active &&
                      ((item.Scope == WatchlistItemScope.Edition && isbns.Contains(item.Isbn13)) ||
                       (item.Scope == WatchlistItemScope.Work && workIds.Contains(item.WorkId)))
                select new { WatchlistId = watchlist.Id, item.Scope, item.Isbn13, item.WorkId })
            .ToListAsync(ct);

        foreach (var key in items)
        {
            counts[key.RequestId] = matches
                .Where(match =>
                    (match.Scope == WatchlistItemScope.Edition && match.Isbn13 == key.Isbn13) ||
                    (key.WorkId is not null &&
                     match.Scope == WatchlistItemScope.Work &&
                     string.Equals(match.WorkId, key.WorkId, StringComparison.OrdinalIgnoreCase)))
                .Select(match => match.WatchlistId)
                .Distinct()
                .Count();
        }

        return counts;
    }
}
