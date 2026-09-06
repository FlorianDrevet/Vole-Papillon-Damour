using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Vole_Papillon_Damour.Application.Books.Commands.Admin;
using Vole_Papillon_Damour.Application.Books.Commands.ScanBook;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.tests.Books.Commands.ScanBook;
using Vole_Papillon_Damour.Domain.BookAggregate.Entities;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.BookMovementAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.Common.Errors;
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
                ClientGestureId: Guid.NewGuid()),
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
                ClientGestureId: Guid.NewGuid()),
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
                ClientGestureId: Guid.NewGuid()),
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
}
