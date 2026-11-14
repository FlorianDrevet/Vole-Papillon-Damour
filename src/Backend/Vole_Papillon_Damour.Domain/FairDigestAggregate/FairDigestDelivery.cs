using Vole_Papillon_Damour.Domain.Common;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Domain.FairDigestAggregate;

public sealed class FairDigestDelivery
{
    public Guid CampaignId { get; private set; }
    public UserId UserId { get; private set; } = null!;
    public FairDigestDeliveryStatus Status { get; private set; }
    public DateTime? SentAt { get; private set; }
    public string? Error { get; private set; }

    private FairDigestDelivery()
    {
    }

    public static FairDigestDelivery Create(Guid campaignId, UserId userId)
    {
        if (campaignId == Guid.Empty)
        {
            throw new ArgumentException("A campaign identifier is required.", nameof(campaignId));
        }

        ArgumentNullException.ThrowIfNull(userId);
        if (userId.Value == Guid.Empty)
        {
            throw new ArgumentException("A valid user identifier is required.", nameof(userId));
        }

        return new FairDigestDelivery
        {
            CampaignId = campaignId,
            UserId = userId,
            Status = FairDigestDeliveryStatus.Pending,
        };
    }

    public void MarkSent(DateTime sentAt)
    {
        EnsurePending();
        SentAt = DomainTime.RequireUtc(sentAt, nameof(sentAt));
        Error = null;
        Status = FairDigestDeliveryStatus.Sent;
    }

    public void MarkSkippedNoContent()
    {
        EnsurePending();
        Status = FairDigestDeliveryStatus.SkippedNoContent;
    }

    public void MarkFailed(string error)
    {
        EnsurePending();
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        var normalizedError = error.Trim();
        if (normalizedError.Length > 128)
        {
            throw new ArgumentException("The delivery error cannot exceed 128 characters.", nameof(error));
        }

        Error = normalizedError;
        Status = FairDigestDeliveryStatus.Failed;
    }

    private void EnsurePending()
    {
        if (Status != FairDigestDeliveryStatus.Pending)
        {
            throw new InvalidOperationException("A completed delivery cannot be changed.");
        }
    }
}
