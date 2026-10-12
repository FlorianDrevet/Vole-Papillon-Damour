using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Vole_Papillon_Damour.Application.Books.Commands.Admin;
using Vole_Papillon_Damour.Application.Books.Commands.ScanBook;
using Vole_Papillon_Damour.Application.Books.Commands.ScanSession;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.tests.Books.Commands.ScanBook;
using Vole_Papillon_Damour.Domain.BookAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate.Entities;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.BookMovementAggregate;
using Vole_Papillon_Damour.Domain.BookMovementAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.Common.Errors;
using Vole_Papillon_Damour.Domain.ManualRequestAggregate;
using Vole_Papillon_Damour.Domain.ManualRequestAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.tests.Books.Commands.Admin;

public sealed class ScanSessionAdministrationCommandHandlerTests
{
    private static readonly UserId AdministratorId =
        UserId.Create(Guid.Parse("00000000-0000-0000-0000-000000000002"));

    [Fact]
    public async Task RemoveMovement_ReversesTheEntryAndPreventsASecondRemoval()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var session = await fixture.AddSessionAsync(ScanMode.AvailableNow);
        var scanResult = await fixture.CreateHandler().Handle(
            new ScanBookCommand(
                session.Id,
                "9782070363735",
                Kept: true,
                OccurredAt: ScanBookCommandHandlerTests.ClientScanAt,
                ClientGestureId: Guid.NewGuid(),
                VolunteerId: session.VolunteerId),
            CancellationToken.None);
        var movement = await fixture.Context.BookMovements.SingleAsync();
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(ScanBookCommandHandlerTests.ReceivedAt);
        var handler = new RemoveScanSessionMovementCommandHandler(
            fixture.Context,
            clock,
            fixture.AlertOutbox);

        var result = await handler.Handle(
            new RemoveScanSessionMovementCommand(session.Id, movement.Id.Value, AdministratorId),
            CancellationToken.None);

        scanResult.IsError.Should().BeFalse();
        result.IsError.Should().BeFalse();
        result.Value.AffectedMovementCount.Should().Be(1);
        (await fixture.Context.Books.SingleAsync()).QuantityAvailable.Should().Be(0);
        (await fixture.Context.ScanSessions.SingleAsync()).Status.Should().Be(ScanSessionStatus.Resumed);
        var reversal = await fixture.Context.BookMovements
            .Where(candidate => candidate.ReversalOfMovementId == movement.Id)
            .SingleAsync();
        reversal.Type.Should().Be(BookMovementType.Correction);
        reversal.Quantity.Should().Be(-1);

        var retry = await handler.Handle(
            new RemoveScanSessionMovementCommand(session.Id, movement.Id.Value, AdministratorId),
            CancellationToken.None);

        retry.IsError.Should().BeTrue();
        retry.FirstError.Code.Should().Be("Book.SessionMovementNotFound");
    }

    [Fact]
    public async Task RemoveMovement_WhenEntryWasConsumed_ReturnsConflictWithoutChangingTheLedger()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var session = await fixture.AddSessionAsync(ScanMode.AvailableNow);
        await fixture.CreateHandler().Handle(
            new ScanBookCommand(
                session.Id,
                "9782070363735",
                Kept: true,
                OccurredAt: ScanBookCommandHandlerTests.ClientScanAt,
                ClientGestureId: Guid.NewGuid(),
                VolunteerId: session.VolunteerId),
            CancellationToken.None);
        var movement = await fixture.Context.BookMovements.SingleAsync();
        var book = await fixture.Context.Books.SingleAsync();
        book.ApplyQuantityCorrection(0, ScanBookCommandHandlerTests.ReceivedAt);
        await fixture.Context.SaveChangesAsync();
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(ScanBookCommandHandlerTests.ReceivedAt);
        var handler = new RemoveScanSessionMovementCommandHandler(
            fixture.Context,
            clock,
            fixture.AlertOutbox);

        var result = await handler.Handle(
            new RemoveScanSessionMovementCommand(session.Id, movement.Id.Value, AdministratorId),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be(Errors.Book.SessionMovementAlreadyConsumed(movement.Id.Value).Code);
        (await fixture.Context.BookMovements.CountAsync()).Should().Be(1);
        (await fixture.Context.ScanSessions.SingleAsync()).Status.Should().Be(ScanSessionStatus.InProgress);
    }

    [Fact]
    public async Task RemoveMovement_ManualRequestHold_RevertsRequestToSearching()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var session = await fixture.AddSessionAsync(ScanMode.AvailableNow);
        var (book, request, movement) = await AddManualHoldAsync(fixture, session, "9782070363735");
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(ScanBookCommandHandlerTests.ReceivedAt);
        var handler = new RemoveScanSessionMovementCommandHandler(
            fixture.Context,
            clock,
            fixture.AlertOutbox);

        var result = await handler.Handle(
            new RemoveScanSessionMovementCommand(session.Id, movement.Id.Value, AdministratorId),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        (await fixture.Context.ManualBookRequests.SingleAsync()).Status.Should().Be(ManualBookRequestStatus.Searching);
        (await fixture.Context.Books.SingleAsync()).QuantityAvailable.Should().Be(0);
        var reversal = await fixture.Context.BookMovements
            .SingleAsync(candidate => candidate.ReversalOfMovementId == movement.Id);
        reversal.Type.Should().Be(BookMovementType.ManualRequestHold);
        reversal.Quantity.Should().Be(-1);
        request.Status.Should().Be(ManualBookRequestStatus.Searching);
        book.QuantityAvailable.Should().Be(0);
    }

    [Fact]
    public async Task RemoveMovement_HoldOfGivenRequest_ReturnsAlreadyConsumed()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var session = await fixture.AddSessionAsync(ScanMode.AvailableNow);
        var (book, request, movement) = await AddManualHoldAsync(
            fixture,
            session,
            "9782070363735",
            markGiven: true);
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(ScanBookCommandHandlerTests.ReceivedAt);
        var handler = new RemoveScanSessionMovementCommandHandler(
            fixture.Context,
            clock,
            fixture.AlertOutbox);

        var result = await handler.Handle(
            new RemoveScanSessionMovementCommand(session.Id, movement.Id.Value, AdministratorId),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be(Errors.Book.SessionMovementAlreadyConsumed(movement.Id.Value).Code);
        request.Status.Should().Be(ManualBookRequestStatus.Given);
        book.QuantityAvailable.Should().Be(0);
        (await fixture.Context.BookMovements.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task CancelSession_ReversesKeptEntriesAndMarksAnnouncementCorrectionsOutOfAvailableStock()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var session = await fixture.AddSessionAsync(ScanMode.NextFair);
        await fixture.CreateHandler().Handle(
            new ScanBookCommand(
                session.Id,
                "9782070363735",
                Kept: true,
                OccurredAt: ScanBookCommandHandlerTests.ClientScanAt,
                ClientGestureId: Guid.NewGuid(),
                VolunteerId: session.VolunteerId),
            CancellationToken.None);
        var movement = await fixture.Context.BookMovements.SingleAsync();
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(ScanBookCommandHandlerTests.ReceivedAt);
        var handler = new CancelScanSessionCommandHandler(
            fixture.Context,
            clock,
            fixture.AlertOutbox);

        var result = await handler.Handle(
            new CancelScanSessionCommand(session.Id, AdministratorId),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.AffectedMovementCount.Should().Be(1);
        (await fixture.Context.ScanSessions.SingleAsync()).Status.Should().Be(ScanSessionStatus.Resumed);
        (await fixture.Context.BookAnnouncements.SingleAsync()).Status
            .Should().Be(BookAnnouncementStatus.Cancelled);
        var reversal = await fixture.Context.BookMovements
            .Where(candidate => candidate.ReversalOfMovementId == movement.Id)
            .SingleAsync();
        reversal.Note.Should().StartWith("Announcement.Correction:");
    }

    [Fact]
    public async Task CancelSession_RevertsFoundHoldsAndSkipsClosedOnes()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var session = await fixture.AddSessionAsync(ScanMode.AvailableNow);
        var (foundBook, foundRequest, foundMovement) = await AddManualHoldAsync(
            fixture,
            session,
            "9782070363735");
        var (givenBook, givenRequest, givenMovement) = await AddManualHoldAsync(
            fixture,
            session,
            "9783140464079",
            markGiven: true);
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(ScanBookCommandHandlerTests.ReceivedAt);
        var handler = new CancelScanSessionCommandHandler(
            fixture.Context,
            clock,
            fixture.AlertOutbox);

        var result = await handler.Handle(
            new CancelScanSessionCommand(session.Id, AdministratorId),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.AffectedMovementCount.Should().Be(1);
        foundRequest.Status.Should().Be(ManualBookRequestStatus.Searching);
        givenRequest.Status.Should().Be(ManualBookRequestStatus.Given);
        foundBook.QuantityAvailable.Should().Be(0);
        givenBook.QuantityAvailable.Should().Be(0);
        (await fixture.Context.BookMovements
            .SingleAsync(movement => movement.ReversalOfMovementId == foundMovement.Id))
            .Type.Should().Be(BookMovementType.ManualRequestHold);
        (await fixture.Context.BookMovements
            .AnyAsync(movement => movement.ReversalOfMovementId == givenMovement.Id))
            .Should().BeFalse();
    }

    [Fact]
    public async Task ForceClose_UsesTheSessionOwnerForTheSharedCloseHandlerAndStoresAdminForced()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var session = await fixture.AddSessionAsync(ScanMode.AvailableNow);
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(ScanBookCommandHandlerTests.ReceivedAt);
        var closeHandler = new CloseScanSessionCommandHandler(fixture.Context, clock, fixture.AlertOutbox);
        var handler = new ForceCloseScanSessionCommandHandler(fixture.Context, closeHandler);

        var result = await handler.Handle(
            new ForceCloseScanSessionCommand(session.Id, AdministratorId),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.CloseReason.Should().Be(ScanCloseReason.AdminForced);
        (await fixture.Context.ScanSessions.SingleAsync()).Status.Should().Be(ScanSessionStatus.Completed);
        await fixture.AlertOutbox.Received(1).QueueForSessionAsync(
            session.Id,
            ScanBookCommandHandlerTests.ReceivedAt,
            Arg.Any<CancellationToken>());
    }

    private static async Task<(Book Book, ManualBookRequest Request, BookMovement Movement)> AddManualHoldAsync(
        ScanBookFixture fixture,
        Vole_Papillon_Damour.Domain.ScanSessionAggregate.ScanSession session,
        string isbn,
        bool markGiven = false)
    {
        var occurredAt = ScanBookCommandHandlerTests.ClientScanAt;
        var book = await fixture.AddBookAsync(isbn, quantityAvailable: 0);
        var requesterId = Guid.NewGuid();
        var request = ManualBookRequest.CreateEdition(
            Guid.NewGuid(),
            requesterId,
            ParseIsbn(isbn),
            "Livre demandé",
            null,
            null,
            null,
            occurredAt.AddMinutes(-1));
        var movement = BookMovement.Create(
            BookMovementId.CreateUnique(),
            book.Id,
            BookMovementType.ManualRequestHold,
            1,
            occurredAt,
            occurredAt.AddMinutes(1),
            clockSuspect: false,
            session.Id,
            session.VolunteerId,
            session.TargetAssoEventsId,
            note: null,
            clientGestureId: Guid.NewGuid(),
            reversalOfMovementId: null);
        var requester = ManualRequester.Create(
            requesterId,
            "Martin",
            "Jeanne",
            "0612345678",
            consentGiven: true,
            AdministratorId,
            occurredAt.AddMinutes(-1));
        request.MarkFoundByScan(book.Id, session.Id, movement.Id, session.VolunteerId, occurredAt);
        if (markGiven)
        {
            request.MarkGiven(AdministratorId, occurredAt.AddMinutes(2));
        }

        fixture.Context.ManualRequesters.Add(requester);
        fixture.Context.ManualBookRequests.Add(request);
        fixture.Context.BookMovements.Add(movement);
        await fixture.Context.SaveChangesAsync();
        return (book, request, movement);
    }

    private static Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects.Isbn13 ParseIsbn(string value)
    {
        Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects.Isbn13.TryCreate(value, out var isbn)
            .Should().BeTrue();
        return isbn;
    }
}
