using Vole_Papillon_Damour.Domain.Common;
using Vole_Papillon_Damour.Domain.Common.Models;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Domain.AssociationSettingsAggregate;

public sealed class AssociationSettings : AggregateRoot<byte>
{
    public const byte SingletonId = 1;

    public int DuplicateThreshold { get; private set; }
    public int DemandSalesThreshold { get; private set; }
    public int DeadStockMinAgeDays { get; private set; }
    public int DeadStockMinQuantity { get; private set; }
    public int WatchlistMaxItems { get; private set; }
    public int AlertCooldownDays { get; private set; }
    public int SessionIdleTimeoutMinutes { get; private set; }
    public int AlertDelayMinutes { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public UserId UpdatedBy { get; private set; } = null!;

    private AssociationSettings(UserId updatedBy, DateTime updatedAt) : base(SingletonId)
    {
        SetValues(5, 1, 30, 1, 100, 30, 120, 120, updatedBy, updatedAt);
    }

    public static AssociationSettings Create(UserId updatedBy, DateTime updatedAt)
    {
        return new AssociationSettings(updatedBy, updatedAt);
    }

    public AssociationSettings()
    {
    }

    public void Update(
        int duplicateThreshold,
        int demandSalesThreshold,
        int deadStockMinAgeDays,
        int deadStockMinQuantity,
        int watchlistMaxItems,
        int alertCooldownDays,
        int sessionIdleTimeoutMinutes,
        int alertDelayMinutes,
        UserId updatedBy,
        DateTime updatedAt)
    {
        SetValues(
            duplicateThreshold,
            demandSalesThreshold,
            deadStockMinAgeDays,
            deadStockMinQuantity,
            watchlistMaxItems,
            alertCooldownDays,
            sessionIdleTimeoutMinutes,
            alertDelayMinutes,
            updatedBy,
            updatedAt);
    }

    private void SetValues(
        int duplicateThreshold,
        int demandSalesThreshold,
        int deadStockMinAgeDays,
        int deadStockMinQuantity,
        int watchlistMaxItems,
        int alertCooldownDays,
        int sessionIdleTimeoutMinutes,
        int alertDelayMinutes,
        UserId updatedBy,
        DateTime updatedAt)
    {
        ValidatePositive(duplicateThreshold, nameof(duplicateThreshold));
        ValidatePositive(demandSalesThreshold, nameof(demandSalesThreshold));
        ValidateNonNegative(deadStockMinAgeDays, nameof(deadStockMinAgeDays));
        ValidateNonNegative(deadStockMinQuantity, nameof(deadStockMinQuantity));
        ValidateNonNegative(watchlistMaxItems, nameof(watchlistMaxItems));
        ValidateNonNegative(alertCooldownDays, nameof(alertCooldownDays));
        ValidatePositive(sessionIdleTimeoutMinutes, nameof(sessionIdleTimeoutMinutes));
        ValidateNonNegative(alertDelayMinutes, nameof(alertDelayMinutes));

        UpdatedBy = updatedBy ?? throw new ArgumentNullException(nameof(updatedBy));
        UpdatedAt = DomainTime.RequireUtc(updatedAt, nameof(updatedAt));
        DuplicateThreshold = duplicateThreshold;
        DemandSalesThreshold = demandSalesThreshold;
        DeadStockMinAgeDays = deadStockMinAgeDays;
        DeadStockMinQuantity = deadStockMinQuantity;
        WatchlistMaxItems = watchlistMaxItems;
        AlertCooldownDays = alertCooldownDays;
        SessionIdleTimeoutMinutes = sessionIdleTimeoutMinutes;
        AlertDelayMinutes = alertDelayMinutes;
    }

    private static void ValidateNonNegative(int value, string parameterName)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, "The value cannot be negative.");
        }
    }

    private static void ValidatePositive(int value, string parameterName)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, "The value must be positive.");
        }
    }
}
