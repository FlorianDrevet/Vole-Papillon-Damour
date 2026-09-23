using FluentAssertions;
using Vole_Papillon_Damour.Domain.CheckoutPassageAggregate;
using Vole_Papillon_Damour.Domain.CheckoutPassageAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Domain.tests.CheckoutPassages;

public sealed class CheckoutPassageTests
{
    private static readonly DateTime Now = new(2026, 3, 14, 15, 0, 0, DateTimeKind.Utc);
    private static readonly UserId Camille = UserId.Create(Guid.NewGuid());
    private static readonly UserId Volunteer = UserId.Create(Guid.NewGuid());

    [Fact]
    public void Associate_IsIdempotentForSameMember()
    {
        var passage = CheckoutPassage.OpenFromSale(Guid.NewGuid(), Now, Now);

        passage.Associate(Camille, Volunteer, Now).Should().BeTrue();
        passage.Associate(Camille, Volunteer, Now.AddMinutes(5)).Should().BeFalse();

        passage.Status.Should().Be(CheckoutPassageStatus.Associated);
        passage.AssociatedAt.Should().Be(Now);
        passage.IsVisibleToMember.Should().BeTrue();
    }

    [Fact]
    public void Associate_ToAnotherMemberAfterAssociation_Throws()
    {
        var passage = CheckoutPassage.OpenFromSale(Guid.NewGuid(), Now, Now);
        passage.Associate(Camille, Volunteer, Now);

        var act = () => passage.Associate(UserId.Create(Guid.NewGuid()), Volunteer, Now);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Associate_AfterDissociation_IsIgnored()
    {
        var passage = CheckoutPassage.OpenFromSale(Guid.NewGuid(), Now, Now);
        passage.Associate(Camille, Volunteer, Now);
        passage.Dissociate(UserId.Create(Guid.NewGuid()), Now.AddDays(1));

        passage.Associate(Camille, Volunteer, Now.AddDays(2)).Should().BeFalse();

        passage.UserId.Should().BeNull();
        passage.IsVisibleToMember.Should().BeFalse();
    }

    [Fact]
    public void MarkUnresolved_KeepsPassageAnonymous()
    {
        var passage = CheckoutPassage.OpenFromAssociation(Guid.NewGuid(), Now, Now);

        passage.MarkUnresolved("card-not-recognised", Now);

        passage.Status.Should().Be(CheckoutPassageStatus.Unresolved);
        passage.UserId.Should().BeNull();
    }

    [Fact]
    public void Anonymize_RemovesEveryPersonalLink()
    {
        var passage = CheckoutPassage.OpenFromSale(Guid.NewGuid(), Now, Now);
        passage.Associate(Camille, Volunteer, Now);

        passage.Anonymize(Now.AddDays(10));

        passage.UserId.Should().BeNull();
        passage.AssociatedByVolunteerId.Should().BeNull();
        passage.DissociatedByUserId.Should().BeNull();
        passage.Status.Should().Be(CheckoutPassageStatus.Dissociated);
        passage.IsVisibleToMember.Should().BeFalse();
    }

    [Fact]
    public void MarkUnresolved_OnlyAllowsPendingAssociation()
    {
        var passage = CheckoutPassage.OpenFromSale(Guid.NewGuid(), Now, Now);
        passage.Associate(Camille, Volunteer, Now);

        var act = () => passage.MarkUnresolved("card-not-recognised", Now.AddMinutes(1));

        act.Should().Throw<InvalidOperationException>();
    }
}
