using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Vole_Papillon_Damour.Application.Books.Commands.DeleteBook;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.tests.Books.Commands.ScanBook;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
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
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(ScanBookCommandHandlerTests.ReceivedAt);
        var handler = new DeleteBookCommandHandler(fixture.Context, clock);

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
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(ScanBookCommandHandlerTests.ReceivedAt);
        var handler = new DeleteBookCommandHandler(fixture.Context, clock);

        var result = await handler.Handle(
            new DeleteBookCommand(book.Isbn13.Value, AdministratorId),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Book.BookHasSales");
        (await fixture.Context.Books.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Handle_WhenIncompleteBookHasEntryHistory_ArchivesItAndRemovesAvailableStock()
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
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(ScanBookCommandHandlerTests.ReceivedAt);
        var handler = new DeleteBookCommandHandler(fixture.Context, clock);

        var result = await handler.Handle(
            new DeleteBookCommand(book.Isbn13.Value, AdministratorId),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Deleted.Should().BeTrue();
        var archived = await fixture.Context.Books.SingleAsync();
        archived.MetadataStatus.Should().Be(BookMetadataStatus.Discarded);
        archived.IsHiddenFromCatalog.Should().BeTrue();
        archived.QuantityAvailable.Should().Be(0);
        var movements = await fixture.Context.BookMovements.OrderBy(movement => movement.Type).ToListAsync();
        movements.Should().HaveCount(2);
        movements.Should().ContainSingle(movement => movement.Type == BookMovementType.Withdrawal && movement.Quantity == -1);
    }

    [Fact]
    public async Task Handle_WhenCompleteBookHasNonSaleHistory_RefusesDeletionToPreserveLedger()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var book = await fixture.AddBookAsync("9782070363735", quantityAvailable: 1);
        book.ApplyManualMetadata(
            new BookMetadataPatch("Livre complet", null, null, null, null, null, null, null,
                [BookMetadataField.Title]),
            ScanBookCommandHandlerTests.ReceivedAt);
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
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(ScanBookCommandHandlerTests.ReceivedAt);
        var handler = new DeleteBookCommandHandler(fixture.Context, clock);

        var result = await handler.Handle(
            new DeleteBookCommand(book.Isbn13.Value, AdministratorId),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Book.BookHasHistory");
    }
}
