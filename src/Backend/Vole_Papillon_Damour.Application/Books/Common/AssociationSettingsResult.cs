using Vole_Papillon_Damour.Domain.AssociationSettingsAggregate;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Books.Common;

public sealed record AssociationSettingsResult(
    int DuplicateThreshold,
    int DemandSalesThreshold,
    int DeadStockMinAgeDays,
    int DeadStockMinQuantity,
    int WatchlistMaxItems,
    int AlertCooldownDays,
    int SessionIdleTimeoutMinutes,
    int AlertDelayMinutes,
    DateTime UpdatedAt,
    UserId UpdatedBy)
{
    public static AssociationSettingsResult From(AssociationSettings settings)
    {
        return new AssociationSettingsResult(
            settings.DuplicateThreshold,
            settings.DemandSalesThreshold,
            settings.DeadStockMinAgeDays,
            settings.DeadStockMinQuantity,
            settings.WatchlistMaxItems,
            settings.AlertCooldownDays,
            settings.SessionIdleTimeoutMinutes,
            settings.AlertDelayMinutes,
            settings.UpdatedAt,
            settings.UpdatedBy);
    }
}
