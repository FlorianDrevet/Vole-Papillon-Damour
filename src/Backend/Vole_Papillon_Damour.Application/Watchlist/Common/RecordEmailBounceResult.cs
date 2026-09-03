using Vole_Papillon_Damour.Domain.WatchlistAggregate;
using Vole_Papillon_Damour.Domain.WatchlistAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.WatchlistFeature.Common;

public sealed record RecordEmailBounceResult(
    int BounceCount,
    WatchlistAlertStatus AlertStatus)
{
    public static RecordEmailBounceResult From(Watchlist watchlist)
    {
        return new RecordEmailBounceResult(
            watchlist.BounceCount,
            watchlist.AlertStatus);
    }
}
