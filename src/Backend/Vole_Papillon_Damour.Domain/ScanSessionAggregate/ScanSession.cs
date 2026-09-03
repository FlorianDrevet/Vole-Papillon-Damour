using Vole_Papillon_Damour.Domain.Common;
using Vole_Papillon_Damour.Domain.Common.Models;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Domain.ScanSessionAggregate;

public sealed class ScanSession : AggregateRoot<ScanSessionId>
{
    public UserId VolunteerId { get; private set; } = null!;
    public ScanMode Mode { get; private set; }
    public AssoEventsId? TargetAssoEventsId { get; private set; }
    public DateTime StartedAt { get; private set; }
    public DateTime LastScanAt { get; private set; }
    public DateTime LastSyncAt { get; private set; }
    public bool LateArrivals { get; private set; }
    public DateTime? EndedAt { get; private set; }
    public ScanCloseReason? CloseReason { get; private set; }
    public ScanSessionStatus Status { get; private set; }
    public int ScannedCount { get; private set; }
    public int KeptCount { get; private set; }
    public int RejectedCount { get; private set; }

    private ScanSession(UserId volunteerId, ScanMode mode, AssoEventsId? targetAssoEventsId, DateTime startedAt)
        : base(ScanSessionId.CreateUnique())
    {
        VolunteerId = volunteerId ?? throw new ArgumentNullException(nameof(volunteerId));
        Mode = mode;
        TargetAssoEventsId = targetAssoEventsId;
        StartedAt = DomainTime.RequireUtc(startedAt, nameof(startedAt));
        LastScanAt = StartedAt;
        LastSyncAt = StartedAt;
        Status = ScanSessionStatus.InProgress;
    }

    public static ScanSession Create(
        UserId volunteerId,
        ScanMode mode,
        AssoEventsId? targetAssoEventsId,
        DateTime startedAt)
    {
        return new ScanSession(volunteerId, mode, targetAssoEventsId, startedAt);
    }

    public ScanSession()
    {
    }

    public void RecordScan(bool kept, DateTime clientScanAt, DateTime syncAt)
    {
        if (Status != ScanSessionStatus.InProgress)
        {
            throw new InvalidOperationException("A completed scan session cannot receive scans.");
        }

        LastScanAt = DomainTime.RequireUtc(clientScanAt, nameof(clientScanAt));
        LastSyncAt = DomainTime.RequireUtc(syncAt, nameof(syncAt));
        ScannedCount++;

        if (kept)
        {
            KeptCount++;
        }
        else
        {
            RejectedCount++;
        }
    }

    public bool Close(ScanCloseReason closeReason, DateTime endedAt)
    {
        if (Status != ScanSessionStatus.InProgress)
        {
            return false;
        }

        EndedAt = DomainTime.RequireUtc(endedAt, nameof(endedAt));
        CloseReason = closeReason;
        Status = ScanSessionStatus.Completed;
        return true;
    }

    public bool Reassign(ScanMode mode, AssoEventsId? targetAssoEventsId)
    {
        if (Status != ScanSessionStatus.Completed)
        {
            return false;
        }

        if (mode == ScanMode.AvailableNow && targetAssoEventsId is not null)
        {
            throw new ArgumentException(
                "AvailableNow sessions cannot target a fair.",
                nameof(targetAssoEventsId));
        }

        Mode = mode;
        TargetAssoEventsId = targetAssoEventsId;
        Status = ScanSessionStatus.Resumed;
        return true;
    }
}
