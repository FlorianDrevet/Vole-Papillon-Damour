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
}
