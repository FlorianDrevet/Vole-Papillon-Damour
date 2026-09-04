using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.Common.Services;
using Vole_Papillon_Damour.Application.WatchlistFeature.Commands.AddWatchlistItem;
using Vole_Papillon_Damour.Application.WatchlistFeature.Commands.RemoveWatchlistItem;
using Vole_Papillon_Damour.Application.WatchlistFeature.Common;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.WatchlistAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.tests.WatchlistFeature;

public sealed class WatchlistCommandHandlerTests
{
    private static readonly Guid MemberId =
        Guid.Parse("0f6a7307-ef0d-4b25-b8d1-814f58d5ab62");
    private static readonly Guid OtherMemberId =
        Guid.Parse("8b7aef2f-a7fd-49f0-8d6b-51a6792a0c84");

    [Fact]
    public async Task AddWorkItem_CreatesTheMemberProjectionAndWatchlist()
    {
        await using var fixture = await WatchlistFeatureTestFixture.CreateAsync();
        var handler = CreateAddHandler(fixture);

        var result = await handler.Handle(
            new AddWatchlistItemCommand(
                MemberId,
                "member@example.test",
                WatchlistItemScope.Work,
                "work-42",
                null),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Scope.Should().Be(WatchlistItemScope.Work);
        result.Value.WorkId.Should().Be("work-42");
        result.Value.Isbn13.Should().BeNull();
        (await fixture.Context.Users.FindAsync(UserId.Create(MemberId))).Should().NotBeNull();
        (await fixture.Context.Watchlists.FindAsync(UserId.Create(MemberId))).Should().NotBeNull();
        (await fixture.Context.WatchlistItems.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task AddItem_WhenTheSameTargetExists_ReturnsConflict()
    {
        await using var fixture = await WatchlistFeatureTestFixture.CreateAsync();
        var handler = CreateAddHandler(fixture);
        var command = new AddWatchlistItemCommand(
            MemberId,
            "member@example.test",
            WatchlistItemScope.Edition,
            null,
            "9782070363735");

        (await handler.Handle(command, CancellationToken.None)).IsError.Should().BeFalse();
        var result = await handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Watchlist.DuplicateItem");
        (await fixture.Context.WatchlistItems.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task AddItem_WhenTheLimitIsReached_ReturnsConflict()
    {
        await using var fixture = await WatchlistFeatureTestFixture.CreateAsync(watchlistMaxItems: 1);
        var handler = CreateAddHandler(fixture);

        (await handler.Handle(
            new AddWatchlistItemCommand(
                MemberId,
                "member@example.test",
                WatchlistItemScope.Work,
                "work-1",
                null),
            CancellationToken.None)).IsError.Should().BeFalse();
        var result = await handler.Handle(
            new AddWatchlistItemCommand(
                MemberId,
                "member@example.test",
                WatchlistItemScope.Work,
                "work-2",
                null),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Watchlist.LimitReached");
    }

    [Fact]
    public async Task AddItem_WhenTheDuplicateIsAlsoAtTheLimit_ReturnsDuplicateConflict()
    {
        await using var fixture = await WatchlistFeatureTestFixture.CreateAsync(watchlistMaxItems: 1);
        var handler = CreateAddHandler(fixture);
        var command = new AddWatchlistItemCommand(
            MemberId,
            "member@example.test",
            WatchlistItemScope.Work,
            "work-1",
            null);

        (await handler.Handle(command, CancellationToken.None)).IsError.Should().BeFalse();
        var result = await handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Watchlist.DuplicateItem");
    }

    [Fact]
    public async Task RemoveItem_CannotRemoveAnotherMemberItem()
    {
        await using var fixture = await WatchlistFeatureTestFixture.CreateAsync();
        var addHandler = CreateAddHandler(fixture);
        var added = await addHandler.Handle(
            new AddWatchlistItemCommand(
                MemberId,
                "member@example.test",
                WatchlistItemScope.Work,
                "work-42",
                null),
            CancellationToken.None);
        var removeHandler = CreateRemoveHandler(fixture);

        var result = await removeHandler.Handle(
            new RemoveWatchlistItemCommand(OtherMemberId, "other@example.test", added.Value.Id),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Watchlist.ItemNotFound");
        (await fixture.Context.WatchlistItems.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task RemoveItem_DeletesOnlyTheOwnedEntry()
    {
        await using var fixture = await WatchlistFeatureTestFixture.CreateAsync();
        var addHandler = CreateAddHandler(fixture);
        var added = await addHandler.Handle(
            new AddWatchlistItemCommand(
                MemberId,
                "member@example.test",
                WatchlistItemScope.Work,
                "work-42",
                null),
            CancellationToken.None);
        var removeHandler = CreateRemoveHandler(fixture);

        var result = await removeHandler.Handle(
            new RemoveWatchlistItemCommand(MemberId, "member@example.test", added.Value.Id),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        (await fixture.Context.WatchlistItems.CountAsync()).Should().Be(0);
        (await fixture.Context.Watchlists.FindAsync(UserId.Create(MemberId))).Should().NotBeNull();
    }

    private static AddWatchlistItemCommandHandler CreateAddHandler(
        WatchlistFeatureTestFixture fixture)
    {
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(WatchlistFeatureTestFixture.Now);
        return new AddWatchlistItemCommandHandler(
            fixture.Context,
            new MemberIdentityService(fixture.Context, clock),
            clock);
    }

    private static RemoveWatchlistItemCommandHandler CreateRemoveHandler(
        WatchlistFeatureTestFixture fixture)
    {
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(WatchlistFeatureTestFixture.Now);
        return new RemoveWatchlistItemCommandHandler(
            fixture.Context,
            new MemberIdentityService(fixture.Context, clock));
    }
}
