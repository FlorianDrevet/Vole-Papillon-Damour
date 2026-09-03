using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Vole_Papillon_Damour.Application.Books.Commands.ScanSession;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Domain.Common.Errors;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;
using Vole_Papillon_Damour.Application.tests.Books.Commands.ScanBook;

namespace Vole_Papillon_Damour.Application.tests.Books.Commands.ScanSession;

public sealed class ScanSessionCommandHandlerTests
{
    [Fact]
    public async Task Open_WhenVolunteerHasNoActiveSession_CreatesAnInProgressSession()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(ScanBookCommandHandlerTests.ReceivedAt);
        var handler = new OpenScanSessionCommandHandler(fixture.Context, clock);
        var volunteerId = UserId.Create(Guid.Parse("00000000-0000-0000-0000-000000000002"));

        var result = await handler.Handle(
            new OpenScanSessionCommand(volunteerId, ScanMode.AvailableNow, null),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Mode.Should().Be(ScanMode.AvailableNow);
        result.Value.Status.Should().Be(ScanSessionStatus.InProgress);
        result.Value.StartedAt.Should().Be(ScanBookCommandHandlerTests.ReceivedAt);
        var session = await fixture.Context.ScanSessions.SingleAsync();
        session.VolunteerId.Should().Be(volunteerId);
    }

    [Fact]
    public async Task Open_WhenVolunteerAlreadyHasAnActiveSession_ReturnsConflict()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var volunteerId = UserId.Create(Guid.Parse("00000000-0000-0000-0000-000000000002"));
        await fixture.AddSessionAsync(ScanMode.AvailableNow, volunteerId: volunteerId);
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(ScanBookCommandHandlerTests.ReceivedAt);
        var handler = new OpenScanSessionCommandHandler(fixture.Context, clock);

        var result = await handler.Handle(
            new OpenScanSessionCommand(volunteerId, ScanMode.NextFair, null),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be(Errors.Book.ActiveScanSessionExists(volunteerId).Code);
        (await fixture.Context.ScanSessions.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Open_WhenAvailableModeTargetsAFair_ReturnsValidationError()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(ScanBookCommandHandlerTests.ReceivedAt);
        var handler = new OpenScanSessionCommandHandler(fixture.Context, clock);

        var result = await handler.Handle(
            new OpenScanSessionCommand(
                UserId.Create(Guid.Parse("00000000-0000-0000-0000-000000000002")),
                ScanMode.AvailableNow,
                AssoEventsId.CreateUnique()),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be(Errors.Book.TargetFairOnlyForNextFair().Code);
        (await fixture.Context.ScanSessions.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Close_WhenSessionIsInProgress_StoresTheReasonAndEndTime()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var session = await fixture.AddSessionAsync(ScanMode.AvailableNow);
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(ScanBookCommandHandlerTests.ReceivedAt);
        var alertOutbox = Substitute.For<IBookAlertOutbox>();
        var handler = new CloseScanSessionCommandHandler(fixture.Context, clock, alertOutbox);

        var result = await handler.Handle(
            new CloseScanSessionCommand(session.Id, ScanCloseReason.Disconnect),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Status.Should().Be(ScanSessionStatus.Completed);
        result.Value.CloseReason.Should().Be(ScanCloseReason.Disconnect);
        result.Value.EndedAt.Should().Be(ScanBookCommandHandlerTests.ReceivedAt);
        var persisted = await fixture.Context.ScanSessions.SingleAsync();
        persisted.Status.Should().Be(ScanSessionStatus.Completed);
        persisted.EndedAt.Should().Be(ScanBookCommandHandlerTests.ReceivedAt);
        await alertOutbox.Received(1).QueueForSessionAsync(
            session.Id,
            ScanBookCommandHandlerTests.ReceivedAt,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Close_WhenSessionWasAlreadyClosed_IsIdempotent()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var session = await fixture.AddSessionAsync(ScanMode.AvailableNow);
        var firstClock = Substitute.For<IDateTimeProvider>();
        firstClock.UtcNow.Returns(ScanBookCommandHandlerTests.ReceivedAt);
        var alertOutbox = Substitute.For<IBookAlertOutbox>();
        var handler = new CloseScanSessionCommandHandler(fixture.Context, firstClock, alertOutbox);

        var firstResult = await handler.Handle(
            new CloseScanSessionCommand(session.Id, ScanCloseReason.Manual),
            CancellationToken.None);

        var secondClock = Substitute.For<IDateTimeProvider>();
        secondClock.UtcNow.Returns(ScanBookCommandHandlerTests.ReceivedAt.AddMinutes(10));
        var retryHandler = new CloseScanSessionCommandHandler(fixture.Context, secondClock, alertOutbox);
        var retryResult = await retryHandler.Handle(
            new CloseScanSessionCommand(session.Id, ScanCloseReason.TokenExpired),
            CancellationToken.None);

        firstResult.IsError.Should().BeFalse();
        retryResult.IsError.Should().BeFalse();
        retryResult.Value.EndedAt.Should().Be(ScanBookCommandHandlerTests.ReceivedAt);
        retryResult.Value.CloseReason.Should().Be(ScanCloseReason.Manual);
        (await fixture.Context.ScanSessions.CountAsync()).Should().Be(1);
        await alertOutbox.Received(1).QueueForSessionAsync(
            session.Id,
            ScanBookCommandHandlerTests.ReceivedAt,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Close_WhenAlertQueueFails_RollsBackTheSessionClose()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var session = await fixture.AddSessionAsync(ScanMode.AvailableNow);
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(ScanBookCommandHandlerTests.ReceivedAt);
        var alertOutbox = Substitute.For<IBookAlertOutbox>();
        alertOutbox.QueueForSessionAsync(
                Arg.Any<ScanSessionId>(),
                Arg.Any<DateTime>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException(
                new InvalidOperationException("alert queue unavailable")));
        var handler = new CloseScanSessionCommandHandler(fixture.Context, clock, alertOutbox);

        var action = () => handler.Handle(
            new CloseScanSessionCommand(session.Id, ScanCloseReason.Manual),
            CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>();
        var persisted = await fixture.Context.ScanSessions
            .AsNoTracking()
            .SingleAsync();
        persisted.Status.Should().Be(ScanSessionStatus.InProgress);
        persisted.EndedAt.Should().BeNull();
    }
}
