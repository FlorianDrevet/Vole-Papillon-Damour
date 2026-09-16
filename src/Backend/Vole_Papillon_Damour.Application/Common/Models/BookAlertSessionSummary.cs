namespace Vole_Papillon_Damour.Application.Common.Models;

public sealed record BookAlertSessionSummary(
    int TotalCount,
    int PendingCount,
    int SentCount,
    int CancelledCount,
    int FailedCount,
    DateTime? NextPendingDueAt);
