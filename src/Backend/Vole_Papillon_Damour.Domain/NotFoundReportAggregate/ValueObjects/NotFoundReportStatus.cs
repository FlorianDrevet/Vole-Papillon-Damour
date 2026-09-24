namespace Vole_Papillon_Damour.Domain.NotFoundReportAggregate.ValueObjects;

public enum NotFoundReportStatus : byte
{
    Open = 0,
    Cancelled = 1,
    Found = 2,
    Withdrawn = 3,
    Dismissed = 4,
    Lapsed = 5
}
