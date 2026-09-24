using FluentAssertions;
using NSubstitute;
using Vole_Papillon_Damour.Application.Books.Queries.Admin;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.Common.Services;
using Vole_Papillon_Damour.Application.WatchlistFeature.Commands.AddWatchlistItem;
using Vole_Papillon_Damour.Domain.RareBookAggregate;
using Vole_Papillon_Damour.Domain.RareBookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;
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

    [Fact]
    public async Task Handle_ProjectsFollowedRareBooksAndRareAlertHistory()
    {
        await using var fixture = await WatchlistFeatureTestFixture.CreateAsync();
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(WatchlistFeatureTestFixture.Now);
        var identity = new MemberIdentityService(fixture.Context, clock);
        var rareBook = RareBook.Create(
            "Atlas ancien",
            45m,
            WatchlistFeatureTestFixture.Now.AddDays(-1),
            UserId.Create(MemberId),
            authorMention: "Auteur",
            condition: RareBookCondition.AsNew);
        rareBook.Publish(UserId.Create(MemberId), WatchlistFeatureTestFixture.Now.AddHours(-12));
        fixture.Context.RareBooks.Add(rareBook);
        await fixture.Context.SaveChangesAsync();

        await new AddWatchlistItemCommandHandler(fixture.Context, identity, clock).Handle(
            new AddWatchlistItemCommand(
                MemberId,
                "member@example.test",
                WatchlistItemScope.RareBook,
                null,
                null,
                RareBookId: rareBook.Id.Value),
            CancellationToken.None);
        fixture.Context.UserAlertHistories.Add(
            UserAlertHistory.CreateForRareBook(
                Guid.NewGuid(),
                UserId.Create(MemberId),
                rareBook.Id,
                WatchlistFeatureTestFixture.Now.AddHours(-1)));
        await fixture.Context.SaveChangesAsync();

        var result = await new GetAdminMemberQueryHandler(fixture.Context, clock)
            .Handle(new GetAdminMemberQuery(MemberId), CancellationToken.None);

        result.IsError.Should().BeFalse();
        var followed = result.Value.Watchlist.Should().ContainSingle().Subject;
        followed.Scope.Should().Be(nameof(WatchlistItemScope.RareBook));
        followed.RareBookId.Should().Be(rareBook.Id.Value);
        followed.Title.Should().Be("Atlas ancien");
        followed.LastAlertAt.Should().Be(WatchlistFeatureTestFixture.Now.AddHours(-1));
        var alert = result.Value.Alerts.Should().ContainSingle().Subject;
        alert.RareBookId.Should().Be(rareBook.Id.Value);
        alert.Isbn13.Should().BeNull();
        alert.Title.Should().Be("Atlas ancien");
    }
}
