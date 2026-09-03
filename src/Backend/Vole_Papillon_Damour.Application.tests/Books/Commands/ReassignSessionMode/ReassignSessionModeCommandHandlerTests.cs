using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Vole_Papillon_Damour.Application.Books.Commands.ReassignSessionMode;
using Vole_Papillon_Damour.Application.Books.Commands.ScanBook;
using Vole_Papillon_Damour.Application.Books.Commands.ScanSession;
using Vole_Papillon_Damour.Application.tests.Books.Commands.ScanBook;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.BookMovementAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.tests.Books.Commands.ReassignSessionMode;

public sealed class ReassignSessionModeCommandHandlerTests
{
    private static readonly UserId AdministratorId =
        UserId.Create(Guid.Parse("00000000-0000-0000-0000-000000000002"));

    [Fact]
    public async Task Handle_WhenAvailableSessionIsReassignedToNextFair_ReversesDirectEntriesAndCreatesAnnouncements()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var session = await fixture.AddSessionAsync(ScanMode.AvailableNow, volunteerId: AdministratorId);
        var scanHandler = fixture.CreateHandler();
        var scanResult = await scanHandler.Handle(
            new ScanBookCommand(
                session.Id,
                "9782070363735",
                Kept: true,
                ScanBookCommandHandlerTests.ClientScanAt,
                Guid.NewGuid()),
            CancellationToken.None);
        await fixture.CreateCloseScanSessionHandler().Handle(
            new CloseScanSessionCommand(session.Id, ScanCloseReason.Manual),
            CancellationToken.None);
        fixture.AlertOutbox.CancelPendingForSessionAsync(
                session.Id,
                Arg.Any<CancellationToken>())
            .Returns(1);
        var fair = await fixture.AddFairAsync(
            DateTimeOffset.Parse("2026-09-10T00:00:00+02:00"),
            DateTimeOffset.Parse("2026-09-11T00:00:00+02:00"),
            null,
            null);
        var handler = fixture.CreateReassignSessionModeHandler();

        var result = await handler.Handle(
            new ReassignSessionModeCommand(
                session.Id,
                ScanMode.NextFair,
                fair.Id,
                AdministratorId),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.ReversedMovementCount.Should().Be(1);
        result.Value.ReplayedMovementCount.Should().Be(1);
        result.Value.Mode.Should().Be(ScanMode.NextFair);
        result.Value.TargetAssoEventsId.Should().Be(fair.Id);
        var book = await fixture.Context.Books.SingleAsync();
        book.QuantityAvailable.Should().Be(0);
        var announcement = await fixture.Context.BookAnnouncements.SingleAsync();
        announcement.Status.Should().Be(BookAnnouncementStatus.Announced);
        announcement.AssoEventsId.Should().Be(fair.Id);
        (await fixture.Context.BookMovements.CountAsync()).Should().Be(3);
        (await fixture.Context.BookMovements
                .SingleAsync(movement => movement.Type == BookMovementType.Correction))
            .ReversalOfMovementId.Should().Be(scanResult.Value.ScanSessionId == session.Id
                ? (await fixture.Context.BookMovements
                    .SingleAsync(movement => movement.Type == BookMovementType.DirectEntry)).Id
                : null);
        (await fixture.Context.ScanSessions.SingleAsync()).Status.Should().Be(ScanSessionStatus.Resumed);
        await fixture.AlertOutbox.Received(1).CancelPendingForSessionAsync(
            session.Id,
            Arg.Any<CancellationToken>());
        await fixture.AlertOutbox.Received(2).QueueForSessionAsync(
            session.Id,
            ScanBookCommandHandlerTests.ReceivedAt,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenNextFairSessionIsReassignedToAvailable_CancelsAnnouncementAndAddsStock()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var fair = await fixture.AddFairAsync(
            DateTimeOffset.Parse("2026-09-10T00:00:00+02:00"),
            DateTimeOffset.Parse("2026-09-11T00:00:00+02:00"),
            null,
            null);
        var session = await fixture.AddSessionAsync(
            ScanMode.NextFair,
            fair.Id,
            AdministratorId);
        var scanHandler = fixture.CreateHandler();
        await scanHandler.Handle(
            new ScanBookCommand(
                session.Id,
                "9782070363735",
                Kept: true,
                ScanBookCommandHandlerTests.ClientScanAt,
                Guid.NewGuid()),
            CancellationToken.None);
        await fixture.CreateCloseScanSessionHandler().Handle(
            new CloseScanSessionCommand(session.Id, ScanCloseReason.Manual),
            CancellationToken.None);
        var handler = fixture.CreateReassignSessionModeHandler();

        var result = await handler.Handle(
            new ReassignSessionModeCommand(
                session.Id,
                ScanMode.AvailableNow,
                TargetAssoEventsId: null,
                AdministratorId),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.ReversedMovementCount.Should().Be(1);
        result.Value.ReplayedMovementCount.Should().Be(1);
        var book = await fixture.Context.Books.SingleAsync();
        book.QuantityAvailable.Should().Be(1);
        (await fixture.Context.BookAnnouncements.SingleAsync()).Status
            .Should().Be(BookAnnouncementStatus.Cancelled);
        (await fixture.Context.BookMovements
                .SingleAsync(movement => movement.Type == BookMovementType.DirectEntry &&
                                         movement.ClientGestureId == null))
            .Quantity.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WhenSessionWasAlreadyReassigned_ReturnsConflict()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var session = await fixture.AddSessionAsync(ScanMode.AvailableNow, volunteerId: AdministratorId);
        await fixture.CreateCloseScanSessionHandler().Handle(
            new CloseScanSessionCommand(session.Id, ScanCloseReason.Manual),
            CancellationToken.None);
        var handler = fixture.CreateReassignSessionModeHandler();
        var command = new ReassignSessionModeCommand(
            session.Id,
            ScanMode.NextFair,
            TargetAssoEventsId: null,
            AdministratorId);

        var firstResult = await handler.Handle(command, CancellationToken.None);
        var secondResult = await handler.Handle(command, CancellationToken.None);

        firstResult.IsError.Should().BeFalse();
        secondResult.IsError.Should().BeTrue();
        secondResult.FirstError.Code.Should().Be("Book.ScanSessionAlreadyReassigned");
    }
}
