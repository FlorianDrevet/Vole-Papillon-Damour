using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Books.Commands.DeleteBook;
using Vole_Papillon_Damour.Application.tests.Books.Commands.ScanBook;
using Vole_Papillon_Damour.Domain.BookMovementAggregate;
using Vole_Papillon_Damour.Domain.BookMovementAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.tests.Books.Commands.DeleteBook;

public sealed class DeleteBookCommandHandlerTests
{
    private static readonly UserId AdministratorId =
        UserId.Create(Guid.Parse("00000000-0000-0000-0000-000000000002"));

    [Fact]
    public async Task Handle_WhenBookHasNoHistory_DeletesTheBook()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        await fixture.AddBookAsync("9782070363735", quantityAvailable: 0);
        var handler = new DeleteBookCommandHandler(fixture.Context);

        var result = await handler.Handle(
            new DeleteBookCommand("9782070363735", AdministratorId),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Deleted.Should().BeTrue();
        (await fixture.Context.Books.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenBookHasASale_RefusesDeletion()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var book = await fixture.AddBookAsync("9782070363735", quantityAvailable: 1);
        var sale = BookMovement.Create(
            BookMovementId.CreateUnique(),
            book.Isbn13,
            BookMovementType.Sale,
            -1,
            ScanBookCommandHandlerTests.ClientScanAt,
            ScanBookCommandHandlerTests.ReceivedAt,
            clockSuspect: false,
            scanSessionId: null,
            AdministratorId,
            assoEventsId: null,
            note: null,
            clientGestureId: Guid.NewGuid());
        fixture.Context.BookMovements.Add(sale);
        await fixture.Context.SaveChangesAsync();
        var handler = new DeleteBookCommandHandler(fixture.Context);

        var result = await handler.Handle(
            new DeleteBookCommand(book.Isbn13.Value, AdministratorId),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Book.BookHasSales");
        (await fixture.Context.Books.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Handle_WhenBookHasNonSaleHistory_RefusesDeletionToPreserveLedger()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var book = await fixture.AddBookAsync("9782070363735", quantityAvailable: 1);
        var entry = BookMovement.Create(
            BookMovementId.CreateUnique(),
            book.Isbn13,
            BookMovementType.DirectEntry,
            1,
            ScanBookCommandHandlerTests.ClientScanAt,
            ScanBookCommandHandlerTests.ReceivedAt,
            clockSuspect: false,
            scanSessionId: null,
            AdministratorId,
            assoEventsId: null,
            note: null,
            clientGestureId: Guid.NewGuid());
        fixture.Context.BookMovements.Add(entry);
        await fixture.Context.SaveChangesAsync();
        var handler = new DeleteBookCommandHandler(fixture.Context);

        var result = await handler.Handle(
            new DeleteBookCommand(book.Isbn13.Value, AdministratorId),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Book.BookHasHistory");
    }
}
