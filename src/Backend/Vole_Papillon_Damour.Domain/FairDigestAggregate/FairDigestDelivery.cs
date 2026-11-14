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

}
