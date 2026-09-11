using FluentAssertions;
using NSubstitute;
using Vole_Papillon_Damour.Application.Books.Queries.Admin;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.Common.Models;
using Vole_Papillon_Damour.Application.tests.Books.Commands.ScanBook;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.tests.Books.Queries.Admin;

public sealed class GetAdminScanSessionsQueryHandlerTests
{
    [Fact]
    public async Task Handle_WhenStaleFilterIsRequested_ReturnsOnlyInProgressSessionsOlderThanTwentyFourHours()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var now = new DateTime(2026, 9, 5, 17, 0, 0, DateTimeKind.Utc);
        var staleSession = await fixture.AddSessionAsync(
            ScanMode.AvailableNow,
            volunteerId: null,
            startedAt: now.AddHours(-25));
        await fixture.AddSessionAsync(
            ScanMode.NextFair,
            volunteerId: UserId.Create(Guid.Parse("00000000-0000-0000-0000-000000000002")),
            startedAt: now.AddHours(-23));
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(now);
        fixture.AlertOutbox.GetAdminPageAsync(
                Arg.Any<BookAlertQueueStatus?>(),
                Arg.Any<Guid?>(),
                Arg.Any<Guid?>(),
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>())
            .Returns(new BookAlertOutboxAdminPage([], 0, 1, 1));
        var handler = new GetAdminScanSessionsQueryHandler(
            fixture.Context,
            clock,
            fixture.AlertOutbox);

        var result = await handler.Handle(
            new GetAdminScanSessionsQuery(
                nameof(ScanSessionStatus.InProgress),
                null,
                null,
                Page: 1,
                PageSize: 50,
                OlderThan24Hours: true),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.TotalCount.Should().Be(1);
        result.Value.Sessions.Should().ContainSingle(session => session.Id == staleSession.Id.Value);
    }

    [Fact]
    public async Task Handle_WhenVolunteerNameIsMissing_UsesVolunteerEmailAsDisplayName()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var now = new DateTime(2026, 9, 5, 17, 0, 0, DateTimeKind.Utc);
        var volunteerId = UserId.Create(Guid.Parse("00000000-0000-0000-0000-000000000002"));
        fixture.Context.Users.Add(User.CreateFromExternalIdentity(
            volunteerId,
            volunteerId.Value.ToString(),
            "volunteer@example.test",
            now));
        await fixture.Context.SaveChangesAsync();
        await fixture.AddSessionAsync(ScanMode.AvailableNow, volunteerId: volunteerId, startedAt: now);

        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(now);
        fixture.AlertOutbox.GetAdminPageAsync(
                Arg.Any<BookAlertQueueStatus?>(),
                Arg.Any<Guid?>(),
                Arg.Any<Guid?>(),
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>())
            .Returns(new BookAlertOutboxAdminPage([], 0, 1, 1));
        var handler = new GetAdminScanSessionsQueryHandler(
            fixture.Context,
            clock,
            fixture.AlertOutbox);

        var result = await handler.Handle(
            new GetAdminScanSessionsQuery(null, null, null, Page: 1, PageSize: 50),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Sessions.Should().ContainSingle();
        result.Value.Sessions[0].VolunteerName.Should().Be("volunteer@example.test");
    }
}
