namespace Vole_Papillon_Damour.Application.Common.Models;

public sealed record BookAlertDeliveryResult(
    int ClaimedCount,
    int SentCount,
    int CancelledCount,
    int FailedCount,
    bool Disabled);
