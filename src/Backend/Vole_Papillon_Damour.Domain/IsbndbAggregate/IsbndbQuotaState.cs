using Vole_Papillon_Damour.Domain.Common;

namespace Vole_Papillon_Damour.Domain.IsbndbAggregate;

public sealed class IsbndbQuotaState
{
    public const int SingletonId = 1;

    public int Id { get; private set; } = SingletonId;
    public int DailyLimit { get; private set; } = 5_000;
    public int DailyRemaining { get; private set; } = 5_000;
    public DateTime? ResetsAt { get; private set; }
    public DateTime? ObservedAt { get; private set; }
    public DateTime? ExhaustedUntil { get; private set; }
    public DateTime? LastFailureAt { get; private set; }
    public string? LastFailureReason { get; private set; }

    public IsbndbQuotaState()
    {
    }

    public void Observe(int limit, int remaining, DateTime resetsAt, DateTime at)
    {
        if (limit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), "The daily limit must be positive.");
        }

        if (remaining < 0 || remaining > limit)
        {
            throw new ArgumentOutOfRangeException(
                nameof(remaining),
                "The remaining quota must be between zero and the daily limit.");
        }

        var utcResetsAt = DomainTime.RequireUtc(resetsAt, nameof(resetsAt));
        var utcAt = DomainTime.RequireUtc(at, nameof(at));
        DailyLimit = limit;
        DailyRemaining = remaining;
        ResetsAt = utcResetsAt;
        ObservedAt = utcAt;

        if (remaining == 0)
        {
            ExhaustedUntil = utcResetsAt;
        }
        else if (ExhaustedUntil is { } exhaustedUntil && utcAt >= exhaustedUntil)
        {
            ExhaustedUntil = null;
        }

    }

    public void MarkExhausted(DateTime until, DateTime at)
    {
        var utcUntil = DomainTime.RequireUtc(until, nameof(until));
        var utcAt = DomainTime.RequireUtc(at, nameof(at));
        if (utcUntil <= utcAt)
        {
            throw new ArgumentOutOfRangeException(nameof(until), "The reset time must be in the future.");
        }

        DailyRemaining = 0;
        ResetsAt = utcUntil;
        ExhaustedUntil = utcUntil;
        ObservedAt = utcAt;
    }

    public void RecordFailure(string reason, DateTime at)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("A failure reason is required.", nameof(reason));
        }

        LastFailureAt = DomainTime.RequireUtc(at, nameof(at));
        LastFailureReason = reason.Trim() is { Length: > 500 } normalized
            ? normalized[..500]
            : reason.Trim();
    }

    public bool IsExhausted(DateTime now)
    {
        var utcNow = DomainTime.RequireUtc(now, nameof(now));
        return ExhaustedUntil is { } exhaustedUntil && exhaustedUntil > utcNow;
    }

    public IsbndbQuotaStatus Status(DateTime now)
    {
        var utcNow = DomainTime.RequireUtc(now, nameof(now));
        if (IsExhausted(utcNow))
        {
            return IsbndbQuotaStatus.Exhausted;
        }

        if (LastFailureAt is { } lastFailureAt &&
            (ObservedAt is null || ObservedAt < lastFailureAt) &&
            utcNow >= lastFailureAt &&
            utcNow < lastFailureAt.AddMinutes(15))
        {
            return IsbndbQuotaStatus.Unavailable;
        }

        return IsbndbQuotaStatus.Operational;
    }
}
