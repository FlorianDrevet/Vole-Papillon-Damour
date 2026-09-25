using Vole_Papillon_Damour.Domain.Common;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Domain.RecommendationAggregate;

/// <summary>N'existe que si le membre a changé la valeur par défaut (activée).</summary>
public sealed class MemberRecommendationPreference
{
    public UserId UserId { get; private set; } = null!;
    public bool Enabled { get; private set; }
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
}
