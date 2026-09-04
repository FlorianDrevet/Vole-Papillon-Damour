using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Books.Commands.RegisterSale;
using Vole_Papillon_Damour.Application.Books.Commands.VoidSale;
using Vole_Papillon_Damour.Domain.BookAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate.Entities;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.BookMovementAggregate;
using Vole_Papillon_Damour.Domain.BookMovementAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;
using Vole_Papillon_Damour.Application.tests.Books.Commands.ScanBook;

namespace Vole_Papillon_Damour.Application.tests.Books.Commands.RegisterSale;

public sealed class RegisterSaleCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenBookIsAvailable_AttachesSaleToTheSingleOpenFair()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var book = await fixture.AddBookAsync("9782070363735", quantityAvailable: 1);
        var fair = await fixture.AddFairAsync(
            DateTimeOffset.Parse("2026-09-03T00:00:00+02:00"),
            DateTimeOffset.Parse("2026-09-04T00:00:00+02:00"),
            DateTimeOffset.Parse("2026-09-03T18:00:00+02:00"),
            DateTimeOffset.Parse("2026-09-03T20:00:00+02:00"));
        var handler = fixture.CreateRegisterSaleHandler();

        var result = await handler.Handle(
            CreateCommand(book.Isbn13.Value),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Isbn13.Should().Be(book.Isbn13.Value);
        result.Value.Quantity.Should().Be(1);
        result.Value.QuantityAvailable.Should().Be(0);
        result.Value.SalesCount.Should().Be(1);
        result.Value.AssoEventsId.Should().Be(fair.Id);
        result.Value.FairMatchStatus.Should().Be(SaleFairMatchStatus.Attached);
        result.Value.HadNoAvailableStock.Should().BeFalse();
        result.Value.HadUnreleasedAnnouncement.Should().BeFalse();
        result.Value.AlreadyProcessed.Should().BeFalse();

        var movement = await fixture.Context.BookMovements.SingleAsync();
        movement.Type.Should().Be(BookMovementType.Sale);
        movement.Quantity.Should().Be(-1);
        movement.AssoEventsId.Should().Be(fair.Id);
        movement.VolunteerId.Should().Be(CreateCommand(book.Isbn13.Value).VolunteerId);
    }

    [Fact]
    public async Task Handle_WhenAvailableQuantityIsZero_RecordsSaleAndSignalsInventoryGap()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var book = await fixture.AddBookAsync("9782070363735", quantityAvailable: 0);
        var session = await fixture.AddSessionAsync(ScanMode.NextFair);
        var announcement = BookAnnouncement.Create(
            BookAnnouncementId.CreateUnique(),
            book.Isbn13,
            assoEventsId: null,
            quantity: 1,
            ScanBookCommandHandlerTests.ClientScanAt,
            session.Id);
        fixture.Context.BookAnnouncements.Add(announcement);
        await fixture.Context.SaveChangesAsync();
        await fixture.AddFairAsync(
            DateTimeOffset.Parse("2026-09-03T00:00:00+02:00"),
            DateTimeOffset.Parse("2026-09-04T00:00:00+02:00"),
            DateTimeOffset.Parse("2026-09-03T18:00:00+02:00"),
            DateTimeOffset.Parse("2026-09-03T20:00:00+02:00"));
        var handler = fixture.CreateRegisterSaleHandler();

        var result = await handler.Handle(
            CreateCommand(book.Isbn13.Value),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.QuantityAvailable.Should().Be(0);
        result.Value.SalesCount.Should().Be(1);
        result.Value.HadNoAvailableStock.Should().BeTrue();
        result.Value.HadUnreleasedAnnouncement.Should().BeTrue();
        (await fixture.Context.BookMovements.SingleAsync()).Quantity.Should().Be(-1);
    }

    [Fact]
    public async Task Handle_WhenGestureIsRetried_DoesNotRecordAnotherSale()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var book = await fixture.AddBookAsync("9782070363735", quantityAvailable: 1);
        var command = CreateCommand(book.Isbn13.Value);
        var handler = fixture.CreateRegisterSaleHandler();

        var firstResult = await handler.Handle(command, CancellationToken.None);
        var retryResult = await handler.Handle(command, CancellationToken.None);

        firstResult.IsError.Should().BeFalse();
        retryResult.IsError.Should().BeFalse();
        retryResult.Value.AlreadyProcessed.Should().BeTrue();
        (await fixture.Context.Books.SingleAsync()).SalesCount.Should().Be(1);
        (await fixture.Context.Books.SingleAsync()).QuantityAvailable.Should().Be(0);
        (await fixture.Context.BookMovements.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Handle_WhenNoFairIsOpen_PersistsSaleWithoutGuessingAnAttachment()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var book = await fixture.AddBookAsync("9782070363735", quantityAvailable: 1);
        var handler = fixture.CreateRegisterSaleHandler();

        var result = await handler.Handle(
            CreateCommand(book.Isbn13.Value),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.AssoEventsId.Should().BeNull();
        result.Value.FairMatchStatus.Should().Be(SaleFairMatchStatus.NoOpenFair);
        (await fixture.Context.BookMovements.SingleAsync()).Note
            .Should().Be("Sale.NoOpenFair");
    }

    [Fact]
    public async Task Handle_WhenOnlyOpenFairIsCancelled_PersistsSaleWithoutGuessingAnAttachment()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var book = await fixture.AddBookAsync("9782070363735", quantityAvailable: 1);
        var cancelledFair = await fixture.AddFairAsync(
            DateTimeOffset.Parse("2026-09-03T00:00:00+02:00"),
            DateTimeOffset.Parse("2026-09-04T00:00:00+02:00"),
            DateTimeOffset.Parse("2026-09-03T18:00:00+02:00"),
            DateTimeOffset.Parse("2026-09-03T20:00:00+02:00"));
        cancelledFair.Cancel().Should().BeTrue();
        await fixture.Context.SaveChangesAsync();
        var handler = fixture.CreateRegisterSaleHandler();

        var result = await handler.Handle(
            CreateCommand(book.Isbn13.Value),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.AssoEventsId.Should().BeNull();
        result.Value.FairMatchStatus.Should().Be(SaleFairMatchStatus.NoOpenFair);
        (await fixture.Context.BookMovements.SingleAsync()).Note
            .Should().Be("Sale.NoOpenFair");
    }

    [Fact]
    public async Task Handle_WhenOpenFairsOverlap_PersistsSaleWithoutGuessingAnAttachment()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var book = await fixture.AddBookAsync("9782070363735", quantityAvailable: 1);
        await fixture.AddFairAsync(
            DateTimeOffset.Parse("2026-09-03T00:00:00+02:00"),
            DateTimeOffset.Parse("2026-09-04T00:00:00+02:00"),
            DateTimeOffset.Parse("2026-09-03T18:00:00+02:00"),
            DateTimeOffset.Parse("2026-09-03T20:00:00+02:00"));
        await fixture.AddFairAsync(
            DateTimeOffset.Parse("2026-09-03T00:00:00+02:00"),
            DateTimeOffset.Parse("2026-09-04T00:00:00+02:00"),
            DateTimeOffset.Parse("2026-09-03T18:30:00+02:00"),
            DateTimeOffset.Parse("2026-09-03T21:00:00+02:00"));
        var handler = fixture.CreateRegisterSaleHandler();

        var result = await handler.Handle(
            CreateCommand(book.Isbn13.Value),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.AssoEventsId.Should().BeNull();
        result.Value.FairMatchStatus.Should().Be(SaleFairMatchStatus.OverlappingOpenFairs);
        (await fixture.Context.BookMovements.SingleAsync()).Note
            .Should().Be("Sale.OverlappingOpenFairs");
    }

    [Fact]
    public async Task Handle_WhenClientTimestampIsInTheFuture_UsesServerTimeAndMarksClockSuspect()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var book = await fixture.AddBookAsync("9782070363735", quantityAvailable: 1);
        var command = CreateCommand(
            book.Isbn13.Value,
            occurredAt: ScanBookCommandHandlerTests.ReceivedAt.AddMinutes(1));
        var handler = fixture.CreateRegisterSaleHandler();

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.ClockSuspect.Should().BeTrue();
        var movement = await fixture.Context.BookMovements.SingleAsync();
        movement.OccurredAt.Should().Be(ScanBookCommandHandlerTests.ReceivedAt);
        movement.ReceivedAt.Should().Be(ScanBookCommandHandlerTests.ReceivedAt);
    }

    [Fact]
    public async Task Handle_WhenSaleIsVoidedWhileFairIsOpen_RecordsInverseMovementAndRestoresQuantity()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var book = await fixture.AddBookAsync("9782070363735", quantityAvailable: 1);
        var fair = await fixture.AddFairAsync(
            DateTimeOffset.Parse("2026-09-03T00:00:00+02:00"),
            DateTimeOffset.Parse("2026-09-04T00:00:00+02:00"),
            DateTimeOffset.Parse("2026-09-03T18:00:00+02:00"),
            DateTimeOffset.Parse("2026-09-03T20:00:00+02:00"));
        var registerHandler = fixture.CreateRegisterSaleHandler();
        var saleCommand = CreateCommand(book.Isbn13.Value);
        var saleResult = await registerHandler.Handle(saleCommand, CancellationToken.None);
        var voidHandler = fixture.CreateVoidSaleHandler();

        var result = await voidHandler.Handle(
            new VoidSaleCommand(
                saleResult.Value.SaleMovementId,
                ScanBookCommandHandlerTests.ClientScanAt,
                saleCommand.VolunteerId,
                Guid.NewGuid()),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.OriginalSaleMovementId.Should().Be(saleResult.Value.SaleMovementId);
        result.Value.QuantityAvailable.Should().Be(1);
        result.Value.SalesCount.Should().Be(0);
        result.Value.AssoEventsId.Should().Be(fair.Id);
        result.Value.AlreadyProcessed.Should().BeFalse();

        var movements = await fixture.Context.BookMovements
            .OrderBy(movement => movement.Type)
            .ToListAsync();
        movements.Should().HaveCount(2);
        movements.Single(movement => movement.Type == BookMovementType.Correction)
            .Should().Match<BookMovement>(movement =>
                movement.Quantity == 1 &&
                movement.ReversalOfMovementId == saleResult.Value.SaleMovementId);
    }

    [Fact]
    public async Task Handle_WhenVoidGestureIsRetried_DoesNotRestoreQuantityTwice()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var book = await fixture.AddBookAsync("9782070363735", quantityAvailable: 1);
        await fixture.AddFairAsync(
            DateTimeOffset.Parse("2026-09-03T00:00:00+02:00"),
            DateTimeOffset.Parse("2026-09-04T00:00:00+02:00"),
            DateTimeOffset.Parse("2026-09-03T18:00:00+02:00"),
            DateTimeOffset.Parse("2026-09-03T20:00:00+02:00"));
        var registerHandler = fixture.CreateRegisterSaleHandler();
        var saleResult = await registerHandler.Handle(
            CreateCommand(book.Isbn13.Value),
            CancellationToken.None);
        var voidCommand = new VoidSaleCommand(
            saleResult.Value.SaleMovementId,
            ScanBookCommandHandlerTests.ClientScanAt,
            CreateCommand(book.Isbn13.Value).VolunteerId,
            Guid.NewGuid());
        var voidHandler = fixture.CreateVoidSaleHandler();

        var firstResult = await voidHandler.Handle(voidCommand, CancellationToken.None);
        var retryResult = await voidHandler.Handle(voidCommand, CancellationToken.None);

        firstResult.IsError.Should().BeFalse();
        retryResult.IsError.Should().BeFalse();
        retryResult.Value.AlreadyProcessed.Should().BeTrue();
        (await fixture.Context.BookMovements.CountAsync()).Should().Be(2);
        (await fixture.Context.Books.SingleAsync()).QuantityAvailable.Should().Be(1);
        (await fixture.Context.Books.SingleAsync()).SalesCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenFairIsClosed_RefusesSaleCancellation()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var book = await fixture.AddBookAsync("9782070363735", quantityAvailable: 1);
        var fair = await fixture.AddFairAsync(
            DateTimeOffset.Parse("2026-09-03T00:00:00+02:00"),
            DateTimeOffset.Parse("2026-09-03T19:30:00+02:00"),
            DateTimeOffset.Parse("2026-09-03T18:00:00+02:00"),
            DateTimeOffset.Parse("2026-09-03T19:30:00+02:00"));
        var registerHandler = fixture.CreateRegisterSaleHandler();
        var saleResult = await registerHandler.Handle(
            CreateCommand(book.Isbn13.Value),
            CancellationToken.None);
        saleResult.Value.AssoEventsId.Should().Be(fair.Id);
        fixture.SetReceivedAt(new DateTime(2026, 9, 3, 17, 31, 0, DateTimeKind.Utc));
        var voidHandler = fixture.CreateVoidSaleHandler();

        var result = await voidHandler.Handle(
            new VoidSaleCommand(
                saleResult.Value.SaleMovementId,
                new DateTime(2026, 9, 3, 17, 1, 0, DateTimeKind.Utc),
                CreateCommand(book.Isbn13.Value).VolunteerId,
                Guid.NewGuid()),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Book.SaleCancellationOutsideOpenFair");
        (await fixture.Context.BookMovements.CountAsync()).Should().Be(1);
    }

    private static RegisterSaleCommand CreateCommand(
        string isbn,
        int quantity = 1,
        DateTime? occurredAt = null,
        Guid? gestureId = null)
    {
        return new RegisterSaleCommand(
            isbn,
            quantity,
            occurredAt ?? ScanBookCommandHandlerTests.ClientScanAt,
            UserId.Create(Guid.Parse("00000000-0000-0000-0000-000000000001")),
            gestureId ?? Guid.NewGuid());
    }

    private static Isbn13 ParseIsbn(string value)
    {
        Isbn13.TryCreate(value, out var isbn).Should().BeTrue();
        return isbn;
    }
}
