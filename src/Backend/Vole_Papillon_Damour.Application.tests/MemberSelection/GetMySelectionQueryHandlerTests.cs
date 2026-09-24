using FluentAssertions;
using Vole_Papillon_Damour.Application.MemberSelection.Commands.AddSelectionItem;
using Vole_Papillon_Damour.Application.MemberSelection.Common;
using Vole_Papillon_Damour.Application.MemberSelection.Queries.GetMySelection;
using Vole_Papillon_Damour.Domain.MemberSelectionAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.tests.MemberSelection;

public sealed class GetMySelectionQueryHandlerTests
{
    private static readonly Guid ExternalId = Guid.Parse("7f0b0f0e-0000-0000-0000-00000000c0de");
    private static readonly DateTime Now = new(2026, 9, 23, 10, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(2, false, false, SelectionAvailability.Available)]
    [InlineData(0, true, false, SelectionAvailability.Announced)]
    [InlineData(0, false, false, SelectionAvailability.OutOfStock)]
    [InlineData(3, false, true, SelectionAvailability.Unavailable)]
    public async Task Handle_ProjectsEditionAvailability(
        int quantity,
        bool announced,
        bool hidden,
        SelectionAvailability expected)
    {
        await using var fixture = await MemberSelectionFixture.CreateAsync(Now);
        await fixture.AddBookAsync("9782070612758", quantityAvailable: quantity, hidden: false);
        if (announced)
        {
            await fixture.AddAnnouncementAsync("9782070612758");
        }

        await fixture.CreateAddHandler().Handle(Add(isbn: "9782070612758"), default);
        if (hidden)
        {
            await fixture.HideBookAsync("9782070612758");
        }

        var result = await fixture.CreateGetMySelectionHandler().Handle(Query(), default);

        result.Value.Items.Should().ContainSingle()
            .Which.Availability.Should().Be(expected);
    }

    [Fact]
    public async Task Handle_WhenRareBookSold_ReturnsRareSold()
    {
        await using var fixture = await MemberSelectionFixture.CreateAsync(Now);
        var rareBook = await fixture.AddRareBookAsync();
        await fixture.CreateAddHandler().Handle(Add(rareBookId: rareBook.Id.Value), default);
        await fixture.MarkRareBookSoldAsync(rareBook.Id);

        var result = await fixture.CreateGetMySelectionHandler().Handle(Query(), default);

        result.Value.Items.Single().Availability.Should().Be(SelectionAvailability.RareSold);
    }

    [Fact]
    public async Task Handle_ReturnsOnlyOwnItems_NewestFirst()
    {
        await using var fixture = await MemberSelectionFixture.CreateAsync(Now);
        await fixture.AddBookAsync("9782070612758");
        await fixture.AddBookAsync("9782070584628");
        await fixture.AddSelectionItemForOtherMemberAsync("9782070612758");
        await fixture.CreateAddHandler().Handle(Add(isbn: "9782070612758"), default);
        fixture.Clock.Advance(TimeSpan.FromMinutes(1));
        await fixture.CreateAddHandler().Handle(Add(isbn: "9782070584628"), default);

        var result = await fixture.CreateGetMySelectionHandler().Handle(Query(), default);

        result.Value.Items.Select(item => item.Isbn13).Should().Equal("9782070584628", "9782070612758");
    }

    [Fact]
    public async Task Handle_WhenEmpty_ReturnsEmptyList()
    {
        await using var fixture = await MemberSelectionFixture.CreateAsync(Now);

        var result = await fixture.CreateGetMySelectionHandler().Handle(Query(), default);

        result.IsError.Should().BeFalse();
        result.Value.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_IncludesTheNextBookFairSummary()
    {
        await using var fixture = await MemberSelectionFixture.CreateAsync(Now);
        var nextFair = await fixture.AddFairAsync(new DateTimeOffset(Now.AddDays(30), TimeSpan.Zero));

        var result = await fixture.CreateGetMySelectionHandler().Handle(Query(), default);

        result.Value.NextFair.Should().BeEquivalentTo(
            new NextFairSummary(nextFair.Id.Value, nextFair.DateStart));
    }

    private static AddSelectionItemCommand Add(string? isbn = null, Guid? rareBookId = null) =>
        new(ExternalId, "camille@example.test", "Camille", null, isbn, rareBookId);

    private static GetMySelectionQuery Query() =>
        new(ExternalId, "camille@example.test", "Camille", null);
}

