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
    public DateTime UpdatedAt { get; private set; }

    private Watchlist(UserId userId, DateTime createdAt) : base(userId)
    {
        if (userId is null || userId.Value == Guid.Empty)
        {
            throw new ArgumentException("A valid user identifier is required.", nameof(userId));
        }

        CreatedAt = DomainTime.RequireUtc(createdAt, nameof(createdAt));
        UpdatedAt = CreatedAt;
    }

    public static Watchlist Create(UserId userId, DateTime createdAt)
    {
        return new Watchlist(userId, createdAt);
    }

    public Watchlist()
    {
    }

    public bool AlertsEnabled => AlertStatus == WatchlistAlertStatus.Active;

    public void SuspendAlerts(DateTime updatedAt)
    {
        AlertStatus = WatchlistAlertStatus.Suspended;
        UpdatedAt = DomainTime.RequireUtc(updatedAt, nameof(updatedAt));
    }

    public void ActivateAlerts(DateTime updatedAt)
    {
        AlertStatus = WatchlistAlertStatus.Active;
        UpdatedAt = DomainTime.RequireUtc(updatedAt, nameof(updatedAt));
    }

    public void BlockAlerts(DateTime updatedAt)
    {
        AlertStatus = WatchlistAlertStatus.Blocked;
        UpdatedAt = DomainTime.RequireUtc(updatedAt, nameof(updatedAt));
    }

    public void RecordEmailBounce(DateTime updatedAt)
    {
        BounceCount++;
        if (BounceCount >= BounceSuspensionThreshold && AlertStatus != WatchlistAlertStatus.Blocked)
        {
            AlertStatus = WatchlistAlertStatus.Suspended;
        }

        UpdatedAt = DomainTime.RequireUtc(updatedAt, nameof(updatedAt));
    }

    public void RecordSuccessfulEmailDelivery(DateTime updatedAt)
    {
        BounceCount = 0;
        UpdatedAt = DomainTime.RequireUtc(updatedAt, nameof(updatedAt));
    }
}
