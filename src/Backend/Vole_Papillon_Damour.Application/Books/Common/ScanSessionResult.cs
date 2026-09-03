using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Books.Common;

public sealed record ScanSessionResult(
    ScanSessionId ScanSessionId,
    UserId VolunteerId,
    ScanMode Mode,
    AssoEventsId? TargetAssoEventsId,
    DateTime StartedAt,
    DateTime LastScanAt,
    DateTime LastSyncAt,
    bool LateArrivals,
    DateTime? EndedAt,
    ScanCloseReason? CloseReason,
    ScanSessionStatus Status,
    int ScannedCount,
    int KeptCount,
    int RejectedCount)
{
    public static ScanSessionResult From(ScanSession session)
    {
        return new ScanSessionResult(
            session.Id,
            session.VolunteerId,
            session.Mode,
            session.TargetAssoEventsId,
            session.StartedAt,
            session.LastScanAt,
            session.LastSyncAt,
            session.LateArrivals,
            session.EndedAt,
            session.CloseReason,
            session.Status,
            session.ScannedCount,
            session.KeptCount,
            session.RejectedCount);
    }
}
