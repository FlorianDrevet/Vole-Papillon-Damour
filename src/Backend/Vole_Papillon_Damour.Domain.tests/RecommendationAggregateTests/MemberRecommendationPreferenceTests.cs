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
}
