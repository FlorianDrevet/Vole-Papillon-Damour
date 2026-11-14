using Vole_Papillon_Damour.Domain.Common;
using Vole_Papillon_Damour.Domain.Common.Models;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Domain.FairDigestAggregate;

public sealed class FairDigestCampaign : AggregateRoot<Guid>
{
    public Guid CampaignId => Id;
    public AssoEventsId AssoEventsId { get; private set; } = null!;
    public DateTime PlannedAtUtc { get; private set; }
    public DateTime? SuspendedAt { get; private set; }
    public UserId? SuspendedBy { get; private set; }
    public DateTime? StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public int SentCount { get; private set; }
    public int SkippedCount { get; private set; }
    public int FailedCount { get; private set; }

    private FairDigestCampaign(Guid campaignId, AssoEventsId assoEventsId, DateTime plannedAtUtc)
        : base(campaignId)
    {
        if (campaignId == Guid.Empty)
        {
            throw new ArgumentException("A campaign identifier is required.", nameof(campaignId));
        }

        ArgumentNullException.ThrowIfNull(assoEventsId);
        if (assoEventsId.Value == Guid.Empty)
        {
            throw new ArgumentException("A book fair identifier is required.", nameof(assoEventsId));
        }

        AssoEventsId = assoEventsId;
        PlannedAtUtc = DomainTime.RequireUtc(plannedAtUtc, nameof(plannedAtUtc));
    }

    public FairDigestCampaign()
    {
    }

    public static FairDigestCampaign Create(AssoEventsId assoEventsId, DateTime plannedAtUtc)
    {
        return new FairDigestCampaign(Guid.NewGuid(), assoEventsId, plannedAtUtc);
    }

    public void Suspend(UserId by, DateTime at)
    {
        ArgumentNullException.ThrowIfNull(by);
        if (StartedAt is not null || CompletedAt is not null)
        {
            throw new InvalidOperationException("A started or completed campaign cannot be suspended.");
        }

        var utcAt = DomainTime.RequireUtc(at, nameof(at));
        SuspendedAt = utcAt;
        SuspendedBy = by;
    }

    public bool Resume(DateTime at)
    {
        if (StartedAt is not null || CompletedAt is not null)
        {
            throw new InvalidOperationException("A started or completed campaign cannot be resumed.");
        }

        if (SuspendedAt is null && SuspendedBy is null)
        {
            return false;
        }

        _ = DomainTime.RequireUtc(at, nameof(at));
        SuspendedAt = null;
        SuspendedBy = null;
        return true;
    }

    public bool Start(DateTime at)
    {
        if (CompletedAt is not null || StartedAt is not null)
        {
            return false;
        }

        if (SuspendedAt is not null)
        {
            throw new InvalidOperationException("A suspended campaign cannot start.");
        }

        StartedAt = DomainTime.RequireUtc(at, nameof(at));
        return true;
    }

    public bool Complete(DateTime at, int sentCount, int skippedCount, int failedCount)
    {
        if (StartedAt is null)
        {
            throw new InvalidOperationException("A campaign must start before it can complete.");
        }

        if (CompletedAt is not null)
        {
            return false;
        }

        if (sentCount < 0 || skippedCount < 0 || failedCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sentCount), "Campaign counts cannot be negative.");
        }

        var utcAt = DomainTime.RequireUtc(at, nameof(at));
        if (utcAt < StartedAt)
        {
            throw new ArgumentOutOfRangeException(nameof(at), "A campaign cannot complete before it starts.");
        }

        CompletedAt = utcAt;
        SentCount = sentCount;
        SkippedCount = skippedCount;
        FailedCount = failedCount;
        return true;
    }
}
