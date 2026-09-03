using Vole_Papillon_Damour.Domain.Common;
using Vole_Papillon_Damour.Domain.Common.Models;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.WatchlistAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Domain.WatchlistAggregate;

public sealed class Watchlist : AggregateRoot<UserId>
{
    public const int BounceSuspensionThreshold = 3;

    public WatchlistAlertStatus AlertStatus { get; private set; } = WatchlistAlertStatus.Active;
    public int BounceCount { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private Watchlist(UserId userId, DateTime createdAt) : base(userId)
    {
        if (userId is null || userId.Value == Guid.Empty)
        {
            throw new ArgumentException("A valid user identifier is required.", nameof(userId));
        }

        CreatedAt = DomainTime.RequireUtc(createdAt, nameof(createdAt));
    }

    public static Watchlist Create(UserId userId, DateTime createdAt)
    {
        return new Watchlist(userId, createdAt);
    }

    public Watchlist()
    {
    }

    public bool AlertsEnabled => AlertStatus == WatchlistAlertStatus.Active;

    public void SuspendAlerts()
    {
        AlertStatus = WatchlistAlertStatus.Suspended;
    }

    public void ActivateAlerts()
    {
        AlertStatus = WatchlistAlertStatus.Active;
    }

    public void BlockAlerts()
    {
        AlertStatus = WatchlistAlertStatus.Blocked;
    }

    public void RecordEmailBounce()
    {
        BounceCount++;
        if (BounceCount >= BounceSuspensionThreshold && AlertStatus != WatchlistAlertStatus.Blocked)
        {
            AlertStatus = WatchlistAlertStatus.Suspended;
        }
    }

    public void RecordSuccessfulEmailDelivery()
    {
        BounceCount = 0;
    }
}
