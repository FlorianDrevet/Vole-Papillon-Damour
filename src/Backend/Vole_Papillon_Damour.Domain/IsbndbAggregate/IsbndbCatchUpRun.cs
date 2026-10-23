using Vole_Papillon_Damour.Domain.Common;

namespace Vole_Papillon_Damour.Domain.IsbndbAggregate;

public sealed class IsbndbCatchUpRun
{
    public long Id { get; private set; }
    public DateTime StartedAt { get; private set; }
    public DateTime? EndedAt { get; private set; }
    public int Filled { get; private set; }
    public int Absent { get; private set; }
    public int Rechecked { get; private set; }
    public int Remaining { get; private set; }
    public IsbndbCatchUpStopReason? StopReason { get; private set; }

    public IsbndbCatchUpRun()
    {
    }

    private IsbndbCatchUpRun(DateTime startedAt)
    {
        StartedAt = DomainTime.RequireUtc(startedAt, nameof(startedAt));
    }

    public static IsbndbCatchUpRun Create(DateTime startedAt) => new(startedAt);

    public void Finish(
        DateTime endedAt,
        int filled,
        int absent,
        int rechecked,
        int remaining,
        IsbndbCatchUpStopReason stopReason)
    {
        var utcEndedAt = DomainTime.RequireUtc(endedAt, nameof(endedAt));
        if (utcEndedAt < StartedAt)
        {
            throw new ArgumentOutOfRangeException(nameof(endedAt), "The end time cannot precede the start time.");
        }

        if (filled < 0 || absent < 0 || rechecked < 0 || remaining < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(filled), "Catch-up counts cannot be negative.");
        }

        if (!Enum.IsDefined(stopReason))
        {
            throw new ArgumentOutOfRangeException(nameof(stopReason));
        }

        EndedAt = utcEndedAt;
        Filled = filled;
        Absent = absent;
        Rechecked = rechecked;
        Remaining = remaining;
        StopReason = stopReason;
    }
}
