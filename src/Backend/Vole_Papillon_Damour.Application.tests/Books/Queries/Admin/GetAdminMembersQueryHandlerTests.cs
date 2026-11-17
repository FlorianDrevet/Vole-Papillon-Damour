using FluentAssertions;
using NSubstitute;
using Vole_Papillon_Damour.Application.Books.Queries.Admin;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.tests.Books.Commands.ScanBook;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.WatchlistAggregate;
using Vole_Papillon_Damour.Domain.WatchlistAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.tests.Books.Queries.Admin;

public sealed class GetAdminMembersQueryHandlerTests
{
    private static readonly DateTime Now = new(2026, 9, 5, 17, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Handle_FiltersByAlertStatusAndCountsOnlyTheReturnedPage()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        await AddMemberAsync(fixture, "alice@example.test", "ext-alice", Now.AddDays(-1), new Name("Alice", "Durand"),
            isbns: ["9782070363735", "9783140464079"], alertHistories: 1);
        await AddMemberAsync(fixture, "bob@example.test", "ext-bob", Now.AddDays(-2), new Name("Bob", "Martin"),
            suspended: true, isbns: ["9791036377426"]);
        await AddMemberAsync(fixture, "carol@test", "ext-carol", Now.AddDays(-3), null, hasWatchlist: false);
        var handler = CreateHandler(fixture);

        var active = await handler.Handle(new GetAdminMembersQuery(null, "active", 1, 10), CancellationToken.None);
        var firstPage = await handler.Handle(new GetAdminMembersQuery(null, null, 1, 2), CancellationToken.None);
        var secondPage = await handler.Handle(new GetAdminMembersQuery(null, null, 2, 2), CancellationToken.None);

        active.IsError.Should().BeFalse();
        active.Value.TotalCount.Should().Be(1);
        active.Value.Members.Should().ContainSingle(member =>
            member.Email == "alice@example.test" &&
            member.AlertStatus == WatchlistAlertStatus.Active.ToString() &&
            member.WatchlistItemCount == 2 &&
            member.AlertHistoryCount == 1);
        firstPage.IsError.Should().BeFalse();
        firstPage.Value.TotalCount.Should().Be(3);
        firstPage.Value.Members.Select(member => member.Email).Should().Equal("alice@example.test", "bob@example.test");
        firstPage.Value.Members[1].WatchlistItemCount.Should().Be(1);
        firstPage.Value.Members[1].AlertStatus.Should().Be(WatchlistAlertStatus.Suspended.ToString());
        secondPage.IsError.Should().BeFalse();
        secondPage.Value.TotalCount.Should().Be(3);
        secondPage.Value.Members.Should().ContainSingle(member =>
            member.Email == "carol@test" &&
            member.AlertStatus == "None" &&
            member.WatchlistItemCount == 0 &&
            member.AlertHistoryCount == 0 &&
            member.DisplayName == null);
    }

    [Fact]
    public async Task Handle_SearchMatchesEmailExternalIdAndDisplayNameWithLiteralWildcards()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        await AddMemberAsync(fixture, "alice@example.test", "ext-alice", Now.AddDays(-1), new Name("Alice", "Durand"));
        await AddMemberAsync(fixture, "bob@example.test", "ext-bob", Now.AddDays(-2), new Name("Bob", "Martin"));
        await AddMemberAsync(fixture, "carol@test", "ext-carol", Now.AddDays(-3), null, hasWatchlist: false);
        var handler = CreateHandler(fixture);

        var displayName = await handler.Handle(new GetAdminMembersQuery("e dur", null, 1, 10), CancellationToken.None);
        var externalId = await handler.Handle(new GetAdminMembersQuery("EXT-CAROL", null, 1, 10), CancellationToken.None);
        var email = await handler.Handle(new GetAdminMembersQuery("bob@", null, 1, 10), CancellationToken.None);
        var percent = await handler.Handle(new GetAdminMembersQuery("%", null, 1, 10), CancellationToken.None);

        displayName.Value.Members.Select(member => member.Email).Should().Equal("alice@example.test");
        externalId.Value.Members.Select(member => member.Email).Should().Equal("carol@test");
        email.Value.Members.Select(member => member.Email).Should().Equal("bob@example.test");
        percent.IsError.Should().BeFalse();
        percent.Value.TotalCount.Should().Be(0);
    }

    private static GetAdminMembersQueryHandler CreateHandler(ScanBookFixture fixture)
    {
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(Now);
        return new GetAdminMembersQueryHandler(fixture.Context, clock);
    }

    private static async Task<UserId> AddMemberAsync(
        ScanBookFixture fixture,
        string email,
        string externalId,
        DateTime lastSeenAt,
        Name? name,
        bool hasWatchlist = true,
        bool suspended = false,
        string[]? isbns = null,
        int alertHistories = 0)
    {
        var userId = UserId.CreateUnique();
        fixture.Context.Users.Add(User.CreateFromExternalIdentity(userId, externalId, email, lastSeenAt, name));
        if (hasWatchlist)
        {
            var watchlist = Watchlist.Create(userId, lastSeenAt);
            if (suspended)
            {
                watchlist.SuspendAlerts(lastSeenAt);
            }

            fixture.Context.Watchlists.Add(watchlist);
        }

        foreach (var isbn in isbns ?? [])
        {
            fixture.Context.WatchlistItems.Add(WatchlistItem.CreateEdition(
                Guid.NewGuid(),
                userId,
                Isbn13.TryCreate(isbn, out var parsed) ? parsed : throw new InvalidOperationException(isbn),
                Now));
        }

        for (var history = 0; history < alertHistories; history++)
        {
            fixture.Context.UserAlertHistories.Add(UserAlertHistory.Create(
                Guid.NewGuid(),
                userId,
                Isbn13.TryCreate("9782070363735", out var historyIsbn) ? historyIsbn : throw new InvalidOperationException(),
                Now));
        }

        await fixture.Context.SaveChangesAsync();
        return userId;
    }
}
