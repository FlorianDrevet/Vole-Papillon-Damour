using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.Common.Services;
using Vole_Papillon_Damour.Application.Recommendations.Commands.SetRecommendationPreference;
using Vole_Papillon_Damour.Application.Recommendations.Queries.GetRecommendationPreference;
using Vole_Papillon_Damour.Application.tests.CheckoutPassages;
using Vole_Papillon_Damour.Domain.UserAggregate;

namespace Vole_Papillon_Damour.Application.tests.Recommendations;

public sealed class SetRecommendationPreferenceCommandHandlerTests
{
    private static readonly DateTime Now = new(2026, 9, 24, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Handle_CreatesPreferenceOnFirstChangeUpdatesItAndCascadesOnMemberDelete()
    {
        await using var fixture = await CheckoutPassageFixture.CreateAsync(Now);
        var member = await fixture.AddMemberAsync();
        var clock = new MutableClock(Now);
        var identity = new MemberIdentityService(fixture.Context, clock);
        var handler = new SetRecommendationPreferenceCommandHandler(fixture.Context, identity, clock);

        var first = await handler.Handle(CommandFor(member, enabled: false), CancellationToken.None);
        first.IsError.Should().BeFalse();
        var preference = await fixture.Context.MemberRecommendationPreferences.SingleAsync();
        preference.Enabled.Should().BeFalse();
        preference.UpdatedAt.Should().Be(Now);

        clock.UtcNow = Now.AddMinutes(10);
        var second = await handler.Handle(CommandFor(member, enabled: true), CancellationToken.None);
        second.IsError.Should().BeFalse();
        var updated = await fixture.Context.MemberRecommendationPreferences.SingleAsync();
        updated.Enabled.Should().BeTrue();
        updated.UpdatedAt.Should().Be(Now.AddMinutes(10));
        (await fixture.Context.MemberRecommendationPreferences.CountAsync()).Should().Be(1);

        fixture.Context.Users.Remove(await fixture.Context.Users.SingleAsync(user => user.Id == member.Id));
        await fixture.Context.SaveChangesAsync();

        (await fixture.Context.MemberRecommendationPreferences.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task GetPreference_DefaultsToEnabledAndReadsTheStoredChoice()
    {
        await using var fixture = await CheckoutPassageFixture.CreateAsync(Now);
        var member = await fixture.AddMemberAsync();
        var clock = new MutableClock(Now);
        var identity = new MemberIdentityService(fixture.Context, clock);
        var queryHandler = new GetRecommendationPreferenceQueryHandler(
            fixture.Context,
            identity);
        var query = new GetRecommendationPreferenceQuery(
            member.Id.Value.ToString("D"),
            member.Email,
            member.Name?.FirstName,
            member.Name?.LastName);

        var defaultPreference = await queryHandler.Handle(query, CancellationToken.None);
        defaultPreference.IsError.Should().BeFalse();
        defaultPreference.Value.Enabled.Should().BeTrue();

        await new SetRecommendationPreferenceCommandHandler(fixture.Context, identity, clock)
            .Handle(CommandFor(member, enabled: false), CancellationToken.None);

        var storedPreference = await queryHandler.Handle(query, CancellationToken.None);
        storedPreference.IsError.Should().BeFalse();
        storedPreference.Value.Enabled.Should().BeFalse();
    }

    private static SetRecommendationPreferenceCommand CommandFor(User member, bool enabled) => new(
        member.Id.Value.ToString("D"),
        member.Email,
        member.Name?.FirstName,
        member.Name?.LastName,
        enabled);

    private sealed class MutableClock(DateTime utcNow) : IDateTimeProvider
    {
        public DateTime UtcNow { get; set; } = utcNow;
    }
}
