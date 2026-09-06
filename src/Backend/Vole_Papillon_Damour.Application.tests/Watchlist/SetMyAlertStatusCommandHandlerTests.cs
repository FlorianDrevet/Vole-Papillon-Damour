using FluentAssertions;
using NSubstitute;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.Common.Services;
using Vole_Papillon_Damour.Application.WatchlistFeature.Commands.SetMyAlertStatus;
using Vole_Papillon_Damour.Domain.UserAggregate;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.WatchlistAggregate;
using Vole_Papillon_Damour.Domain.WatchlistAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.tests.WatchlistFeature;

public sealed class SetMyAlertStatusCommandHandlerTests
{
    private static readonly Guid MemberId =
        Guid.Parse("0f6a7307-ef0d-4b25-b8d1-814f58d5ab62");

    [Fact]
    public async Task Handle_SuspendsAndReactivatesTheMemberAlerts()
    {
        await using var fixture = await WatchlistFeatureTestFixture.CreateAsync();
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(WatchlistFeatureTestFixture.Now);
        var identity = new MemberIdentityService(fixture.Context, clock);
        var handler = new SetMyAlertStatusCommandHandler(fixture.Context, identity, clock);

        var suspended = await handler.Handle(
            new SetMyAlertStatusCommand(MemberId, "member@example.test", Enabled: false),
            CancellationToken.None);

        suspended.IsError.Should().BeFalse();
        suspended.Value.AlertStatus.Should().Be(WatchlistAlertStatus.Suspended);
        suspended.Value.Changed.Should().BeTrue();

        var activated = await handler.Handle(
            new SetMyAlertStatusCommand(MemberId, "member@example.test", Enabled: true),
            CancellationToken.None);

        activated.IsError.Should().BeFalse();
        activated.Value.AlertStatus.Should().Be(WatchlistAlertStatus.Active);
        activated.Value.Changed.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_DoesNotAllowMemberToOverrideAnAdministrativeBlock()
    {
        await using var fixture = await WatchlistFeatureTestFixture.CreateAsync();
        var user = User.CreateFromExternalIdentity(
            UserId.Create(MemberId),
            MemberId.ToString(),
            "member@example.test",
            WatchlistFeatureTestFixture.Now);
        fixture.Context.Users.Add(user);
        var watchlist = Watchlist.Create(user.Id, WatchlistFeatureTestFixture.Now);
        watchlist.BlockAlerts(WatchlistFeatureTestFixture.Now);
        fixture.Context.Watchlists.Add(watchlist);
        await fixture.Context.SaveChangesAsync();
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(WatchlistFeatureTestFixture.Now);
        var handler = new SetMyAlertStatusCommandHandler(
            fixture.Context,
            new MemberIdentityService(fixture.Context, clock),
            clock);

        var result = await handler.Handle(
            new SetMyAlertStatusCommand(MemberId, "member@example.test", Enabled: true),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Watchlist.AlertsBlocked");
        (await fixture.Context.Watchlists.FindAsync(UserId.Create(MemberId)))!
            .AlertStatus.Should().Be(WatchlistAlertStatus.Blocked);
    }
}
