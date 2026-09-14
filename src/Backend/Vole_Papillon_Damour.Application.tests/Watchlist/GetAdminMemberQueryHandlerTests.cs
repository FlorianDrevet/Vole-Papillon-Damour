using FluentAssertions;
using NSubstitute;
using Vole_Papillon_Damour.Application.Books.Queries.Admin;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.Common.Services;
using Vole_Papillon_Damour.Application.WatchlistFeature.Commands.AddWatchlistItem;
using Vole_Papillon_Damour.Domain.WatchlistAggregate;
using Vole_Papillon_Damour.Domain.WatchlistAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.tests.WatchlistFeature;

public sealed class GetAdminMemberQueryHandlerTests
{
    private static readonly Guid MemberId =
        Guid.Parse("5b0b3b8e-54f2-4a57-9b39-0d2d2d3c4a11");

    [Fact]
    public async Task Handle_ProjectsFollowedWorksAndAlertTitlesWithoutLoadingTheWholeCatalog()
    {
        await using var fixture = await WatchlistFeatureTestFixture.CreateAsync();
        await fixture.AddBookAsync("9782070363735", "work-42", "Livre suivi");
        var alerted = await fixture.AddBookAsync("9782070408504", "work-99", "Livre alerté");
        await fixture.AddBookAsync("9783140464079", "work-7", "Livre sans rapport");
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(WatchlistFeatureTestFixture.Now);
        var identity = new MemberIdentityService(fixture.Context, clock);
        var added = await new AddWatchlistItemCommandHandler(fixture.Context, identity, clock).Handle(
            new AddWatchlistItemCommand(
                MemberId,
                "member@example.test",
                WatchlistItemScope.Work,
                "work-42",
                null),
            CancellationToken.None);
        fixture.Context.UserAlertHistories.Add(
            UserAlertHistory.Create(
                Guid.NewGuid(),
                Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects.UserId.Create(MemberId),
                alerted.Isbn13,
                WatchlistFeatureTestFixture.Now.AddHours(-1)));
        await fixture.Context.SaveChangesAsync();
        var handler = new GetAdminMemberQueryHandler(fixture.Context, clock);

        var result = await handler.Handle(new GetAdminMemberQuery(MemberId), CancellationToken.None);

        added.IsError.Should().BeFalse();
        result.IsError.Should().BeFalse();
        var followed = result.Value.Watchlist.Should().ContainSingle().Subject;
        followed.Isbn13.Should().BeNull();
        followed.Title.Should().Be("Livre suivi");
        followed.QuantityAvailable.Should().Be(2);
        result.Value.Alerts.Should().ContainSingle().Which.Title.Should().Be("Livre alerté");
    }
}
