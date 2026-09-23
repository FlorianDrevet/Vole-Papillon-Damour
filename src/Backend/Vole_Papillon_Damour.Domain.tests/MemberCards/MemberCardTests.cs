using FluentAssertions;
using Vole_Papillon_Damour.Domain.MemberCardAggregate;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Domain.tests.MemberCards;

public sealed class MemberCardTests
{
    private static readonly DateTime Now = new(2026, 9, 23, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Rotate_IncrementsVersionAndReplacesCode()
    {
        var card = MemberCard.Issue(Guid.NewGuid(), UserId.Create(Guid.NewGuid()), "LUNE-4271", Now);

        card.Rotate("ROSE-1180", Now.AddDays(1));

        card.Version.Should().Be(2);
        card.RecoveryCode.Should().Be("ROSE-1180");
        card.RotatedAt.Should().Be(Now.AddDays(1));
        card.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Issue_RejectsNonNormalisedCode()
    {
        var act = () => MemberCard.Issue(Guid.NewGuid(), UserId.Create(Guid.NewGuid()), "lune 4271", Now);

        act.Should().Throw<ArgumentException>();
    }
}
