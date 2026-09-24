using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.MemberSelection.Commands.AddSelectionItem;
using Vole_Papillon_Damour.Application.MemberSelection.Commands.RemoveSelectionItem;
using Vole_Papillon_Damour.Domain.MemberSelectionAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.RareBookAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.tests.MemberSelection;

public sealed class SelectionCommandHandlerTests
{
    private static readonly Guid ExternalId = Guid.Parse("7f0b0f0e-0000-0000-0000-00000000c0de");
    private static readonly DateTime Now = new(2026, 9, 23, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Add_WhenEditionIsInCatalogue_CreatesToTakeItem()
    {
        await using var fixture = await MemberSelectionFixture.CreateAsync(Now);
        await fixture.AddBookAsync("9782070612758");

        var result = await fixture.CreateAddHandler().Handle(Add(isbn: "9782070612758"), default);

        result.IsError.Should().BeFalse();
        result.Value.AlreadyPresent.Should().BeFalse();
        var item = await fixture.Context.MemberSelectionItems.SingleAsync();
        item.Status.Should().Be(MemberSelectionStatus.ToTake);
        (await fixture.Context.WatchlistItems.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Add_WhenAddedTwice_IsIdempotent()
    {
        await using var fixture = await MemberSelectionFixture.CreateAsync(Now);
        await fixture.AddBookAsync("9782070612758");
        var handler = fixture.CreateAddHandler();

        var first = await handler.Handle(Add(isbn: "9782070612758"), default);
        var second = await handler.Handle(Add(isbn: "978-2-07-061275-8"), default);

        second.Value.Id.Should().Be(first.Value.Id);
        second.Value.AlreadyPresent.Should().BeTrue();
        (await fixture.Context.MemberSelectionItems.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Add_WhenOnlyAnnounced_Succeeds()
    {
        await using var fixture = await MemberSelectionFixture.CreateAsync(Now);
        await fixture.AddAnnouncementAsync("9782070612758");

        var result = await fixture.CreateAddHandler().Handle(Add(isbn: "9782070612758"), default);

        result.IsError.Should().BeFalse();
    }

    [Fact]
    public async Task Add_WhenNeverReceived_ReturnsNotInCatalog()
    {
        await using var fixture = await MemberSelectionFixture.CreateAsync(Now);

        var result = await fixture.CreateAddHandler().Handle(Add(isbn: "9782070612758"), default);

        result.FirstError.Code.Should().Be("MemberSelection.NotInCatalog");
    }

    [Fact]
    public async Task Add_WhenHidden_ReturnsNotInCatalog()
    {
        await using var fixture = await MemberSelectionFixture.CreateAsync(Now);
        await fixture.AddBookAsync("9782070612758", hidden: true);

        var result = await fixture.CreateAddHandler().Handle(Add(isbn: "9782070612758"), default);

        result.FirstError.Code.Should().Be("MemberSelection.NotInCatalog");
    }

    [Fact]
    public async Task Add_WhenRedirected_StoresCanonicalIsbn()
    {
        await using var fixture = await MemberSelectionFixture.CreateAsync(Now);
        await fixture.AddBookAsync("9782070584628");
        await fixture.AddBookAsync("9782070612758", redirectTo: "9782070584628");

        await fixture.CreateAddHandler().Handle(Add(isbn: "9782070612758"), default);

        var item = await fixture.Context.MemberSelectionItems.SingleAsync();
        item.Isbn13!.Value.Value.Should().Be("9782070584628");
    }

    [Fact]
    public async Task Add_WhenBothTargetsGiven_ReturnsInvalidTarget()
    {
        await using var fixture = await MemberSelectionFixture.CreateAsync(Now);

        var result = await fixture.CreateAddHandler().Handle(
            Add(isbn: "9782070612758", rareBookId: Guid.NewGuid()), default);

        result.FirstError.Code.Should().Be("MemberSelection.InvalidTarget");
    }

    [Fact]
    public async Task Add_WhenRareBookIsDraft_ReturnsNotInCatalog()
    {
        await using var fixture = await MemberSelectionFixture.CreateAsync(Now);
        var rareBook = await fixture.AddRareBookAsync(published: false);

        var result = await fixture.CreateAddHandler().Handle(Add(rareBookId: rareBook.Id.Value), default);

        result.FirstError.Code.Should().Be("MemberSelection.NotInCatalog");
    }

    [Fact]
    public async Task Add_WhenRareBookIsSoldAndPublished_Succeeds()
    {
        await using var fixture = await MemberSelectionFixture.CreateAsync(Now);
        var rareBook = await fixture.AddRareBookAsync(sold: true);

        var result = await fixture.CreateAddHandler().Handle(Add(rareBookId: rareBook.Id.Value), default);

        result.IsError.Should().BeFalse();
        (await fixture.Context.MemberSelectionItems.SingleAsync()).RareBookId.Should().Be(rareBook.Id);
    }

    [Fact]
    public async Task Remove_WhenItemBelongsToAnotherMember_ReturnsNotFound()
    {
        await using var fixture = await MemberSelectionFixture.CreateAsync(Now);
        var foreignItem = await fixture.AddSelectionItemForOtherMemberAsync("9782070612758");

        var result = await fixture.CreateRemoveHandler().Handle(
            new RemoveSelectionItemCommand(ExternalId, "camille@example.test", "Camille", null, foreignItem.Id), default);

        result.FirstError.Code.Should().Be("MemberSelection.NotFound");
        (await fixture.Context.MemberSelectionItems.CountAsync()).Should().Be(1);
    }

    private static AddSelectionItemCommand Add(string? isbn = null, Guid? rareBookId = null) =>
        new(ExternalId, "camille@example.test", "Camille", null, isbn, rareBookId);
}
