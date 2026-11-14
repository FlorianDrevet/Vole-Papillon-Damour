using FluentAssertions;
using Vole_Papillon_Damour.Domain.RecommendationAggregate;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Domain.tests.RecommendationAggregateTests;

public sealed class MemberRecommendationPreferenceTests
{
    [Fact]
    public void Set_ChangesValueAndDate()
    {
        var first = new DateTime(2026, 9, 25, 10, 0, 0, DateTimeKind.Utc);
        var preference = MemberRecommendationPreference.Create(UserId.Create(Guid.NewGuid()), false, first);

        preference.Set(true, first.AddHours(1));

        preference.Enabled.Should().BeTrue();
        preference.UpdatedAt.Should().Be(first.AddHours(1));
    }

    [Fact]
    public void SetMinimumHearts_WhenOutsideRange_Throws()
    {
        var preference = CreatePreference();

        var act = () => preference.SetMinimumHearts(5);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void FairDigestConsent_CanBeGrantedAndRevoked()
    {
        var preference = CreatePreference();
        var consentAt = new DateTime(2026, 11, 14, 10, 0, 0, DateTimeKind.Utc);

        preference.GrantFairDigestConsent(consentAt);

        preference.FairDigestConsentedAt.Should().Be(consentAt);
        preference.RevokeFairDigestConsent();

        preference.FairDigestConsentedAt.Should().BeNull();
    }

    private static MemberRecommendationPreference CreatePreference()
    {
        var at = new DateTime(2026, 11, 14, 9, 0, 0, DateTimeKind.Utc);
        return MemberRecommendationPreference.Create(UserId.Create(Guid.Parse("16f5e243-34ed-4b96-8986-0a9f11b4982b")), false, at);
    }

}
