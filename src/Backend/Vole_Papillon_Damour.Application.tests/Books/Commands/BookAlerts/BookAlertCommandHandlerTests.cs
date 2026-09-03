using FluentAssertions;
using NSubstitute;
using Vole_Papillon_Damour.Application.Books.Commands.CancelBookAlerts;
using Vole_Papillon_Damour.Application.Books.Commands.ForceBookAlerts;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.tests.Books.Commands.ScanBook;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.tests.Books.Commands.BookAlerts;

public sealed class BookAlertCommandHandlerTests
{
    private static readonly UserId AdministratorId =
        UserId.Create(Guid.Parse("00000000-0000-0000-0000-000000000002"));

    private static readonly DateTime ForcedAt =
        new(2026, 9, 3, 18, 30, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Cancel_WhenSessionExists_DelegatesToTheOutboxAndReturnsAffectedCount()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var session = await fixture.AddSessionAsync(ScanMode.AvailableNow);
        fixture.AlertOutbox.CancelPendingForSessionAsync(
                session.Id,
                Arg.Any<CancellationToken>())
            .Returns(3);
        var handler = new CancelBookAlertsCommandHandler(
            fixture.Context,
            fixture.AlertOutbox);

        var result = await handler.Handle(
            new CancelBookAlertsCommand(session.Id, AdministratorId),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.ScanSessionId.Should().Be(session.Id);
        result.Value.AffectedCount.Should().Be(3);
        await fixture.AlertOutbox.Received(1).CancelPendingForSessionAsync(
            session.Id,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Force_WhenSessionExists_DelegatesTheUtcTimeAndReturnsAffectedCount()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var session = await fixture.AddSessionAsync(ScanMode.AvailableNow);
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(ForcedAt);
        fixture.AlertOutbox.ForcePendingForSessionAsync(
                session.Id,
                ForcedAt,
                Arg.Any<CancellationToken>())
            .Returns(2);
        var handler = new ForceBookAlertsCommandHandler(
            fixture.Context,
            fixture.AlertOutbox,
            clock);

        var result = await handler.Handle(
            new ForceBookAlertsCommand(session.Id, AdministratorId),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.ScanSessionId.Should().Be(session.Id);
        result.Value.AffectedCount.Should().Be(2);
        await fixture.AlertOutbox.Received(1).ForcePendingForSessionAsync(
            session.Id,
            ForcedAt,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Cancel_WhenSessionDoesNotExist_ReturnsNotFoundWithoutTouchingTheOutbox()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var missingSessionId = ScanSessionId.CreateUnique();
        var handler = new CancelBookAlertsCommandHandler(
            fixture.Context,
            fixture.AlertOutbox);

        var result = await handler.Handle(
            new CancelBookAlertsCommand(missingSessionId, AdministratorId),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Book.ScanSessionNotFound");
        await fixture.AlertOutbox.DidNotReceive().CancelPendingForSessionAsync(
            Arg.Any<ScanSessionId>(),
            Arg.Any<CancellationToken>());
    }
}
