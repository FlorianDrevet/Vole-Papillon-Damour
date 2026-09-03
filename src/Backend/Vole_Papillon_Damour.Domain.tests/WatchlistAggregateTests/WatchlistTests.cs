using FluentAssertions;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.WatchlistAggregate;
using Vole_Papillon_Damour.Domain.WatchlistAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Domain.tests.WatchlistAggregateTests;

public sealed class WatchlistTests
{
    private static readonly DateTime CreatedAt =
        new(2026, 9, 3, 17, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_EnablesAlertsForTheMember()
    {
        var userId = UserId.CreateUnique();

        var watchlist = Watchlist.Create(userId, CreatedAt);

        watchlist.Id.Should().Be(userId);
        watchlist.AlertStatus.Should().Be(WatchlistAlertStatus.Active);
        watchlist.AlertsEnabled.Should().BeTrue();
        watchlist.BounceCount.Should().Be(0);
    }

    [Fact]
    public void Suspend_StopsAlertMatchingWithoutDeletingTheList()
    {
        var watchlist = Watchlist.Create(UserId.CreateUnique(), CreatedAt);

        watchlist.SuspendAlerts();

        watchlist.AlertStatus.Should().Be(WatchlistAlertStatus.Suspended);
        watchlist.AlertsEnabled.Should().BeFalse();
    }

    [Fact]
    public void RecordEmailBounce_SuspendsAlertsAfterThreeConsecutiveFailures()
    {
        var watchlist = Watchlist.Create(UserId.CreateUnique(), CreatedAt);

        watchlist.RecordEmailBounce();
        watchlist.RecordEmailBounce();
        watchlist.AlertStatus.Should().Be(WatchlistAlertStatus.Active);
        watchlist.BounceCount.Should().Be(2);

        watchlist.RecordEmailBounce();

        watchlist.AlertStatus.Should().Be(WatchlistAlertStatus.Suspended);
        watchlist.AlertsEnabled.Should().BeFalse();
        watchlist.BounceCount.Should().Be(3);
    }

    [Fact]
    public void RecordSuccessfulEmailDelivery_ResetsConsecutiveBouncesWithoutReactivatingSuspendedAlerts()
    {
        var watchlist = Watchlist.Create(UserId.CreateUnique(), CreatedAt);
        watchlist.RecordEmailBounce();
        watchlist.RecordSuccessfulEmailDelivery();

        watchlist.BounceCount.Should().Be(0);
        watchlist.AlertStatus.Should().Be(WatchlistAlertStatus.Active);

        watchlist.RecordEmailBounce();
        watchlist.RecordEmailBounce();
        watchlist.RecordEmailBounce();
        watchlist.RecordSuccessfulEmailDelivery();

        watchlist.BounceCount.Should().Be(0);
        watchlist.AlertStatus.Should().Be(WatchlistAlertStatus.Suspended);
        watchlist.AlertsEnabled.Should().BeFalse();
    }

    [Fact]
    public void RecordEmailBounce_DoesNotOverrideAnAdministrativeBlock()
    {
        var watchlist = Watchlist.Create(UserId.CreateUnique(), CreatedAt);
        watchlist.BlockAlerts();

        watchlist.RecordEmailBounce();
        watchlist.RecordEmailBounce();
        watchlist.RecordEmailBounce();

        watchlist.AlertStatus.Should().Be(WatchlistAlertStatus.Blocked);
        watchlist.BounceCount.Should().Be(3);
        watchlist.AlertsEnabled.Should().BeFalse();
    }

    [Fact]
    public void CreateEmailBounceEvent_TrimsAndStoresTheProviderIdentity()
    {
        var userId = UserId.CreateUnique();

        var bounceEvent = EmailBounceEvent.Create(
            Guid.NewGuid(),
            "  acs-event-42  ",
            userId,
            CreatedAt);

        bounceEvent.ProviderEventId.Should().Be("acs-event-42");
        bounceEvent.UserId.Should().Be(userId);
        bounceEvent.RecordedAt.Should().Be(CreatedAt);
    }

    [Fact]
    public void CreateEmailBounceEvent_RejectsAnEmptyProviderIdentity()
    {
        var action = () => EmailBounceEvent.Create(
            Guid.NewGuid(),
            " ",
            UserId.CreateUnique(),
            CreatedAt);

        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreateEditionItem_StoresOnlyTheEditionTarget()
    {
        var userId = UserId.CreateUnique();
        var isbn = ParseIsbn("9782070363735");

        var item = WatchlistItem.CreateEdition(
            Guid.NewGuid(),
            userId,
            isbn,
            CreatedAt);

        item.Scope.Should().Be(WatchlistItemScope.Edition);
        item.UserId.Should().Be(userId);
        item.Isbn13.Should().Be(isbn);
        item.WorkId.Should().BeNull();
    }

    [Fact]
    public void CreateWorkItem_StoresOnlyTheWorkTarget()
    {
        var item = WatchlistItem.CreateWork(
            Guid.NewGuid(),
            UserId.CreateUnique(),
            "work-42",
            CreatedAt);

        item.Scope.Should().Be(WatchlistItemScope.Work);
        item.WorkId.Should().Be("work-42");
        item.Isbn13.Should().BeNull();
    }

    [Fact]
    public void CreateAlertHistory_RequiresUtcSendTime()
    {
        var action = () => UserAlertHistory.Create(
            Guid.NewGuid(),
            UserId.CreateUnique(),
            ParseIsbn("9782070363735"),
            CreatedAt.ToLocalTime(),
            Guid.NewGuid());

        action.Should().Throw<ArgumentException>();
    }

    private static Isbn13 ParseIsbn(string value)
    {
        Isbn13.TryCreate(value, out var isbn).Should().BeTrue();
        return isbn;
    }
}
