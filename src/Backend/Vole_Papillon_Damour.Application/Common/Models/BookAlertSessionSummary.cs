namespace Vole_Papillon_Damour.Application.Common.Models;

public sealed record BookAlertSessionSummary(
    int TotalCount,
    int PendingCount,
    DateTime? NextPendingDueAt);
