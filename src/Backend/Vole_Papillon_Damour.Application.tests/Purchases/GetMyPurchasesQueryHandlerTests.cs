using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Common.Services;
using Vole_Papillon_Damour.Application.Purchases.Queries.GetMyPurchases;
using Vole_Papillon_Damour.Application.tests.CheckoutPassages;
using Vole_Papillon_Damour.Domain.BookAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.CheckoutPassageAggregate;
using Vole_Papillon_Damour.Domain.CheckoutPassageAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.MemberSelectionAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate;

namespace Vole_Papillon_Damour.Application.tests.Purchases;

public sealed class GetMyPurchasesQueryHandlerTests
{
    private static readonly DateTime Now = new(2026, 9, 24, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Handle_ReturnsOnlyOwnAssociatedPassages_NewestFirst()
    {
        await using var fixture = await CheckoutPassageFixture.CreateAsync(Now);
        var member = await fixture.AddMemberAsync();
        var otherMember = await fixture.AddMemberAsync();
        await fixture.AddAssociatedPassageAsync(member, Now.AddHours(-2), "9782070612758");
        await fixture.AddAssociatedPassageAsync(member, Now.AddHours(-1), "9782203001015");
        await fixture.AddAssociatedPassageAsync(otherMember, Now, "9782070363735");
        await fixture.AddPendingPassageWithSaleAsync("9782070584628");

        var result = await CreateHandler(fixture).Handle(QueryFor(member), default);

        result.IsError.Should().BeFalse();
        result.Value.Passages.Should().HaveCount(2);
        result.Value.Passages.Select(passage => passage.OccurredAt)
            .Should().BeInDescendingOrder();
        result.Value.Passages.SelectMany(passage => passage.Lines)
            .Should().OnlyContain(line => line.Isbn13 == "9782203001015" || line.Isbn13 == "9782070612758");
    }

    [Fact]
    public async Task Handle_WhenPassageHasNoActiveLine_IsHidden()
    {
        await using var fixture = await CheckoutPassageFixture.CreateAsync(Now);
        var member = await fixture.AddMemberAsync();
        await fixture.AddAssociatedPassageWithoutLinesAsync(member, Now);

        var result = await CreateHandler(fixture).Handle(QueryFor(member), default);

        result.IsError.Should().BeFalse();
        result.Value.Passages.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_KeepsSnapshotWhenBookMetadataChangesLater()
    {
        await using var fixture = await CheckoutPassageFixture.CreateAsync(Now);
        var member = await fixture.AddMemberAsync();
        Isbn13.TryCreate("9782070612758", out var isbn).Should().BeTrue();
        var book = Book.Create(isbn, Now);
        book.ApplyManualMetadata(
            new BookMetadataPatch(
                "Le Petit Prince", "Antoine de Saint-Exupéry", "Gallimard", 1946, "Poche", null, null, null,
                [BookMetadataField.Title, BookMetadataField.Authors, BookMetadataField.Publisher,
                    BookMetadataField.PublicationYear, BookMetadataField.PhysicalFormat]),
            Now);
        fixture.Context.Books.Add(book);
        await fixture.Context.SaveChangesAsync();
        await fixture.AddAssociatedPassageAsync(member, Now, "9782070612758");
        book.ApplyManualMetadata(
            new BookMetadataPatch("Autre", null, null, null, null, null, null, null, [BookMetadataField.Title]),
            Now.AddMinutes(1));
        await fixture.Context.SaveChangesAsync();

        var result = await CreateHandler(fixture).Handle(QueryFor(member), default);

        result.Value.Passages.Single().Lines.Single().Title.Should().Be("Le Petit Prince");
        book.Title.Should().Be("Autre");
    }

    [Fact]
    public async Task Handle_ShowsCancelledLines()
    {
        await using var fixture = await CheckoutPassageFixture.CreateAsync(Now);
        var member = await fixture.AddMemberAsync();
        var passage = await fixture.AddAssociatedPassageAsync(member, Now, "9782070612758");
        var line = await fixture.Context.CheckoutPassageLines.SingleAsync();
        line.Void(Now.AddMinutes(5));
        await fixture.Context.SaveChangesAsync();

        var result = await CreateHandler(fixture).Handle(QueryFor(member), default);

        var purchase = result.Value.Passages.Single();
        purchase.Id.Should().Be(passage.Id);
        purchase.ActiveBookCount.Should().Be(0);
        purchase.Lines.Single().State.Should().Be("Cancelled");
    }

    [Fact]
    public async Task Handle_PaginatesWithStableCursor()
    {
        await using var fixture = await CheckoutPassageFixture.CreateAsync(Now);
        var member = await fixture.AddMemberAsync();
        for (var index = 0; index < 12; index++)
        {
            await fixture.AddAssociatedPassageAsync(
                member, Now.AddMinutes(-index), "9782070612758");
        }

        var first = await CreateHandler(fixture).Handle(QueryFor(member, limit: 10), default);
        var second = await CreateHandler(fixture).Handle(
            QueryFor(member, first.Value.NextCursor, 10), default);

        first.Value.Passages.Should().HaveCount(10);
        first.Value.NextCursor.Should().NotBeNullOrWhiteSpace();
        second.Value.Passages.Should().HaveCount(2);
        second.Value.NextCursor.Should().BeNull();
        first.Value.Passages.Select(passage => passage.Id)
            .Intersect(second.Value.Passages.Select(passage => passage.Id)).Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_HidesUnresolvedAndDissociatedPassages()
    {
        await using var fixture = await CheckoutPassageFixture.CreateAsync(Now);
        var member = await fixture.AddMemberAsync();
        var unresolved = await fixture.AddPendingPassageWithSaleAsync("9782070612758");
        unresolved.MarkUnresolved("card-not-recognised", Now);
        var associated = await fixture.AddAssociatedPassageAsync(member, Now, "9782203001015");
        associated.Dissociate(fixture.VolunteerId, Now.AddMinutes(1));
        await fixture.Context.SaveChangesAsync();

        var result = await CreateHandler(fixture).Handle(QueryFor(member), default);

        result.IsError.Should().BeFalse();
        result.Value.Passages.Should().BeEmpty();
    }

    [Fact]
    public void PurchaseLineResult_HasNoPriceField()
    {
        typeof(PurchaseLineResult).GetProperties().Select(property => property.Name)
            .Should().NotContain(name => name.Contains("Price", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Amount", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Total", StringComparison.OrdinalIgnoreCase));
    }

    private static GetMyPurchasesQueryHandler CreateHandler(CheckoutPassageFixture fixture) => new(
        fixture.Context,
        new MemberIdentityService(fixture.Context, new CheckoutPassageTestClock(Now)));

    private static GetMyPurchasesQuery QueryFor(User member, string? cursor = null, int limit = 10) => new(
        member.Id.Value.ToString("D"),
        member.Email!,
        member.Name?.FirstName,
        member.Name?.LastName,
        cursor,
        limit);
}
