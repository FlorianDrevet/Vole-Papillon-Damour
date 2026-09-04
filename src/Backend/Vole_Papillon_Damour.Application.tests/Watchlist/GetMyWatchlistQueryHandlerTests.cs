using FluentAssertions;
using NSubstitute;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.Common.Services;
using Vole_Papillon_Damour.Application.WatchlistFeature.Commands.AddWatchlistItem;
using Vole_Papillon_Damour.Application.WatchlistFeature.Queries.GetMyWatchlist;
using Vole_Papillon_Damour.Domain.WatchlistAggregate;
using Vole_Papillon_Damour.Domain.WatchlistAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.tests.WatchlistFeature;

public sealed class GetMyWatchlistQueryHandlerTests
{
    private static readonly Guid MemberId =
        Guid.Parse("0f6a7307-ef0d-4b25-b8d1-814f58d5ab62");

    [Fact]
    public async Task Handle_ReturnsPublicBookDetailsAndLastAlertWithoutRecipientData()
    {
        await using var fixture = await WatchlistFeatureTestFixture.CreateAsync();
        var book = await fixture.AddBookAsync("9782070363735", "work-42");
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(WatchlistFeatureTestFixture.Now);
        var identity = new MemberIdentityService(fixture.Context, clock);
        var addHandler = new AddWatchlistItemCommandHandler(fixture.Context, identity, clock);
        var added = await addHandler.Handle(
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
                book.Isbn13,
                WatchlistFeatureTestFixture.Now.AddHours(-1)));
        await fixture.Context.SaveChangesAsync();
        var handler = new GetMyWatchlistQueryHandler(fixture.Context, identity, clock);

        var result = await handler.Handle(
            new GetMyWatchlistQuery(MemberId, "member@example.test"),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.AlertStatus.Should().Be(WatchlistAlertStatus.Active);
        result.Value.Items.Should().ContainSingle();
        var item = result.Value.Items.Single();
        item.Id.Should().Be(added.Value.Id);
        item.Book.Should().NotBeNull();
        item.Book!.Isbn13.Should().Be(book.Isbn13.Value);
        item.Book.Title.Should().Be("Le livre de test");
        item.LastAlertAt.Should().Be(WatchlistFeatureTestFixture.Now.AddHours(-1));
    }

    [Fact]
    public async Task Handle_KeepsAnUnknownWorkTargetInTheList()
    {
        await using var fixture = await WatchlistFeatureTestFixture.CreateAsync();
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(WatchlistFeatureTestFixture.Now);
        var identity = new MemberIdentityService(fixture.Context, clock);
        var addHandler = new AddWatchlistItemCommandHandler(fixture.Context, identity, clock);
        await addHandler.Handle(
            new AddWatchlistItemCommand(
                MemberId,
                "member@example.test",
                WatchlistItemScope.Work,
                "not-yet-received",
                null),
            CancellationToken.None);
        var handler = new GetMyWatchlistQueryHandler(fixture.Context, identity, clock);

        var result = await handler.Handle(
            new GetMyWatchlistQuery(MemberId, "member@example.test"),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Items.Should().ContainSingle();
        result.Value.Items.Single().Book.Should().BeNull();
        result.Value.Items.Single().WorkId.Should().Be("not-yet-received");
    }
}
