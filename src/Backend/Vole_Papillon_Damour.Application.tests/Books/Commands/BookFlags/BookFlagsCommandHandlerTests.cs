using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Books.Commands.BookFlags;
using Vole_Papillon_Damour.Application.tests.Books.Commands.ScanBook;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.tests.Books.Commands.BookFlags;

public sealed class BookFlagsCommandHandlerTests
{
    [Fact]
    public async Task MarkRare_UpdatesRareFlagAndSynchronizationTimestamp()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var book = await fixture.AddBookAsync("9782070363735", quantityAvailable: 1);
        var handler = fixture.CreateMarkBookRareHandler();

        var result = await handler.Handle(
            new MarkBookRareCommand(
                book.Isbn13.Value,
                IsRare: true,
                UserId.Create(Guid.Parse("00000000-0000-0000-0000-000000000002"))),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.IsRare.Should().BeTrue();
        result.Value.Changed.Should().BeTrue();
        (await fixture.Context.Books.SingleAsync()).IsRare.Should().BeTrue();
    }

    [Fact]
    public async Task HideBook_UpdatesCatalogVisibilityWithoutChangingQuantity()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var book = await fixture.AddBookAsync("9782070363735", quantityAvailable: 2);
        var handler = fixture.CreateHideBookHandler();

        var result = await handler.Handle(
            new HideBookCommand(
                book.Isbn13.Value,
                Hidden: true,
                UserId.Create(Guid.Parse("00000000-0000-0000-0000-000000000002"))),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.IsHiddenFromCatalog.Should().BeTrue();
        (await fixture.Context.Books.SingleAsync()).QuantityAvailable.Should().Be(2);
    }

    [Fact]
    public async Task MarkRare_WhenBookDoesNotExist_ReturnsNotFound()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var handler = fixture.CreateMarkBookRareHandler();

        var result = await handler.Handle(
            new MarkBookRareCommand(
                "9782070363735",
                IsRare: true,
                UserId.Create(Guid.Parse("00000000-0000-0000-0000-000000000002"))),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Book.NotFound");
    }
}
