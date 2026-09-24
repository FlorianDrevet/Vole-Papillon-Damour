using FluentAssertions;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.MemberSelectionAggregate;
using Vole_Papillon_Damour.Domain.MemberSelectionAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.RareBookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Domain.tests.MemberSelection;

public sealed class MemberSelectionItemTests
{
    private static readonly UserId Member = UserId.Create(Guid.Parse("00000000-0000-0000-0000-0000000000a1"));
    private static readonly DateTime AddedAt = new(2026, 9, 23, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void CreateForEdition_StartsAsToTake()
    {
        Isbn13.TryCreate("9782070612758", out var isbn).Should().BeTrue();

        var item = MemberSelectionItem.CreateForEdition(Guid.NewGuid(), Member, isbn, AddedAt);

        item.Status.Should().Be(MemberSelectionStatus.ToTake);
        item.Isbn13.Should().Be(isbn);
        item.RareBookId.Should().BeNull();
        item.AddedAt.Should().Be(AddedAt);
        item.StatusChangedAt.Should().Be(AddedAt);
        item.PurchasedAt.Should().BeNull();
    }

    [Fact]
    public void CreateForRareBook_KeepsOnlyTheRareIdentifier()
    {
        var rareBookId = RareBookId.Create(Guid.NewGuid());

        var item = MemberSelectionItem.CreateForRareBook(Guid.NewGuid(), Member, rareBookId, AddedAt);

        item.RareBookId.Should().Be(rareBookId);
        item.Isbn13.Should().BeNull();
    }

    [Fact]
    public void Create_RejectsNonUtcTimestamp()
    {
        Isbn13.TryCreate("9782070612758", out var isbn);

        var act = () => MemberSelectionItem.CreateForEdition(
            Guid.NewGuid(), Member, isbn, DateTime.SpecifyKind(AddedAt, DateTimeKind.Local));

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreateForEdition_RejectsDefaultIsbn()
    {
        var act = () => MemberSelectionItem.CreateForEdition(Guid.NewGuid(), Member, default, AddedAt);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreateForRareBook_RejectsEmptyIdentifier()
    {
        var act = () => MemberSelectionItem.CreateForRareBook(
            Guid.NewGuid(), Member, RareBookId.Create(Guid.Empty), AddedAt);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void MarkPurchased_SetsPurchasedOnceAndKeepsFirstDate()
    {
        Isbn13.TryCreate("9782070612758", out var isbn);
        var item = MemberSelectionItem.CreateForEdition(Guid.NewGuid(), Member, isbn, AddedAt);

        item.MarkPurchased(AddedAt.AddDays(3)).Should().BeTrue();
        item.MarkPurchased(AddedAt.AddDays(5)).Should().BeFalse();

        item.Status.Should().Be(MemberSelectionStatus.Purchased);
        item.PurchasedAt.Should().Be(AddedAt.AddDays(3));
    }

    [Fact]
    public void ChangeStatus_AwayFromPurchased_ClearsPurchaseDate()
    {
        Isbn13.TryCreate("9782070612758", out var isbn);
        var item = MemberSelectionItem.CreateForEdition(Guid.NewGuid(), Member, isbn, AddedAt);
        item.MarkPurchased(AddedAt.AddDays(1));

        item.ChangeStatus(MemberSelectionStatus.ToRevisit, AddedAt.AddDays(2));

        item.Status.Should().Be(MemberSelectionStatus.ToRevisit);
        item.PurchasedAt.Should().BeNull();
        item.StatusChangedAt.Should().Be(AddedAt.AddDays(2));
    }

    [Fact]
    public void ChangeStatus_ToPurchasedManually_RecordsDate()
    {
        Isbn13.TryCreate("9782070612758", out var isbn);
        var item = MemberSelectionItem.CreateForEdition(Guid.NewGuid(), Member, isbn, AddedAt);

        item.ChangeStatus(MemberSelectionStatus.Purchased, AddedAt.AddHours(1));

        item.PurchasedAt.Should().Be(AddedAt.AddHours(1));
    }

    [Fact]
    public void ChangeStatus_RejectsUndefinedValue()
    {
        Isbn13.TryCreate("9782070612758", out var isbn);
        var item = MemberSelectionItem.CreateForEdition(Guid.NewGuid(), Member, isbn, AddedAt);

        var act = () => item.ChangeStatus((MemberSelectionStatus)42, AddedAt);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
