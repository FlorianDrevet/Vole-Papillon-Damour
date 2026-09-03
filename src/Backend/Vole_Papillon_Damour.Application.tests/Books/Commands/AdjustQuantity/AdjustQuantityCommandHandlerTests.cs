using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Books.Commands.AdjustQuantity;
using Vole_Papillon_Damour.Application.tests.Books.Commands.ScanBook;
using Vole_Papillon_Damour.Domain.BookMovementAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.tests.Books.Commands.AdjustQuantity;

public sealed class AdjustQuantityCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenPhysicalCountDiffers_RecordsSignedCorrection()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var book = await fixture.AddBookAsync("9782070363735", quantityAvailable: 2);
        var handler = fixture.CreateAdjustQuantityHandler();

        var result = await handler.Handle(
            new AdjustQuantityCommand(
                book.Isbn13.Value,
                QuantityAvailable: 1,
                "Comptage après la bourse",
                UserId.Create(Guid.Parse("00000000-0000-0000-0000-000000000002"))),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.PreviousQuantityAvailable.Should().Be(2);
        result.Value.QuantityAvailable.Should().Be(1);
        result.Value.Delta.Should().Be(-1);
        result.Value.Changed.Should().BeTrue();

        var persistedBook = await fixture.Context.Books.SingleAsync();
        persistedBook.QuantityAvailable.Should().Be(1);
        persistedBook.SalesCount.Should().Be(0);
        var movement = await fixture.Context.BookMovements.SingleAsync();
        movement.Type.Should().Be(BookMovementType.Correction);
        movement.Quantity.Should().Be(-1);
        movement.Note.Should().Be("Comptage après la bourse");
        movement.ScanSessionId.Should().BeNull();
        movement.ClientGestureId.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenPhysicalCountMatches_DoesNotCreateZeroMovement()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var book = await fixture.AddBookAsync("9782070363735", quantityAvailable: 2);
        var handler = fixture.CreateAdjustQuantityHandler();

        var result = await handler.Handle(
            new AdjustQuantityCommand(
                book.Isbn13.Value,
                QuantityAvailable: 2,
                "Comptage sans écart",
                UserId.Create(Guid.Parse("00000000-0000-0000-0000-000000000002"))),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Delta.Should().Be(0);
        result.Value.Changed.Should().BeFalse();
        result.Value.MovementId.Should().BeNull();
        (await fixture.Context.BookMovements.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenBookDoesNotExist_ReturnsNotFound()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var handler = fixture.CreateAdjustQuantityHandler();

        var result = await handler.Handle(
            new AdjustQuantityCommand(
                "9782070363735",
                QuantityAvailable: 1,
                "Comptage",
                UserId.Create(Guid.Parse("00000000-0000-0000-0000-000000000002"))),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Book.NotFound");
    }
}
