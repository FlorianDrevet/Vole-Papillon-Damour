using Vole_Papillon_Damour.Domain.Common;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Domain.RecommendationAggregate;

/// <summary>N'existe que si le membre a changé la valeur par défaut (activée).</summary>
public sealed class MemberRecommendationPreference
{
    public UserId UserId { get; private set; } = null!;
    public bool Enabled { get; private set; }
    public int? MinimumHearts { get; private set; }
    public DateTime? FairDigestConsentedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private MemberRecommendationPreference()
    {
    }

    public static MemberRecommendationPreference Create(UserId userId, bool enabled, DateTime at)
    {
        ArgumentNullException.ThrowIfNull(userId);
        return new MemberRecommendationPreference
        {
            UserId = userId,
            Enabled = enabled,
            UpdatedAt = DomainTime.RequireUtc(at, nameof(at)),
        };
    }

    public void Set(bool enabled, DateTime at)
    {
        Enabled = enabled;
        UpdatedAt = DomainTime.RequireUtc(at, nameof(at));
    }

    public void SetMinimumHearts(int? minimumHearts)
    {
        if (minimumHearts is < 1 or > 4)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minimumHearts),
                "The minimum number of hearts must be between 1 and 4.");
        }

        MinimumHearts = minimumHearts;
    }

    public void SetMinimumHearts(int? minimumHearts, DateTime at)
    {
        SetMinimumHearts(minimumHearts);
        UpdatedAt = DomainTime.RequireUtc(at, nameof(at));
    }

    public void GrantFairDigestConsent(DateTime now)
    {
        var utcNow = DomainTime.RequireUtc(now, nameof(now));
        FairDigestConsentedAt = utcNow;
        UpdatedAt = utcNow;
    }

    public void RevokeFairDigestConsent()
    {
        FairDigestConsentedAt = null;
    }

    public void RevokeFairDigestConsent(DateTime at)
    {
        RevokeFairDigestConsent();
        UpdatedAt = DomainTime.RequireUtc(at, nameof(at));
    }
}
