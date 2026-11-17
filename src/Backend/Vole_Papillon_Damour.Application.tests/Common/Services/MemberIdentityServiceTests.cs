using FluentAssertions;
using NSubstitute;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.Common.Services;
using Vole_Papillon_Damour.Application.tests.WatchlistFeature;

namespace Vole_Papillon_Damour.Application.tests.Common.Services;

public sealed class MemberIdentityServiceTests
{
    [Fact]
    public async Task EnsureAsync_WhenDisplayNameIsProvided_StoresItForAdministrativeProjections()
    {
        await using var fixture = await WatchlistFeatureTestFixture.CreateAsync();
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(WatchlistFeatureTestFixture.Now);
        var service = new MemberIdentityService(fixture.Context, clock);

        var user = await service.EnsureAsync(
            Guid.Parse("8d12cf6e-958f-4f68-a6f3-914d7bba8cf4"),
            "camille@example.test",
            "Camille Dupont",
            CancellationToken.None);

        user.Name.Should().NotBeNull();
        user.Name!.FirstName.Should().Be("Camille");
        user.Name.LastName.Should().Be("Dupont");
    }

    [Fact]
    public async Task EnsureAsync_WhenUserWasSeenLessThanAnHourAgo_DoesNotWriteLastSeenAt()
    {
        await using var fixture = await WatchlistFeatureTestFixture.CreateAsync();
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(WatchlistFeatureTestFixture.Now, WatchlistFeatureTestFixture.Now.AddMinutes(5));
        var service = new MemberIdentityService(fixture.Context, clock);
        var externalId = Guid.Parse("8d12cf6e-958f-4f68-a6f3-914d7bba8cf4");
        await service.EnsureAsync(externalId, "camille@example.test", CancellationToken.None);

        await service.EnsureAsync(externalId, "camille@example.test", CancellationToken.None);

        fixture.Context.Users.Single().LastSeenAt.Should().Be(WatchlistFeatureTestFixture.Now);
    }

    [Fact]
    public async Task EnsureAsync_WhenUserWasLastSeenMoreThanAnHourAgo_UpdatesLastSeenAt()
    {
        await using var fixture = await WatchlistFeatureTestFixture.CreateAsync();
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(WatchlistFeatureTestFixture.Now, WatchlistFeatureTestFixture.Now.AddMinutes(61));
        var service = new MemberIdentityService(fixture.Context, clock);
        var externalId = Guid.Parse("8d12cf6e-958f-4f68-a6f3-914d7bba8cf4");
        await service.EnsureAsync(externalId, "camille@example.test", "Camille Dupont", CancellationToken.None);

        await service.EnsureAsync(externalId, "camille@example.test", "Camille Dupont", CancellationToken.None);

        fixture.Context.Users.Single().LastSeenAt.Should().Be(WatchlistFeatureTestFixture.Now.AddMinutes(61));
    }

    [Fact]
    public async Task EnsureAsync_WhenUserIsNew_CreatesItWithTheCurrentTime()
    {
        await using var fixture = await WatchlistFeatureTestFixture.CreateAsync();
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(WatchlistFeatureTestFixture.Now);
        var service = new MemberIdentityService(fixture.Context, clock);

        var user = await service.EnsureAsync(
            Guid.Parse("8d12cf6e-958f-4f68-a6f3-914d7bba8cf4"),
            "camille@example.test",
            "Camille Dupont",
            CancellationToken.None);

        user.CreatedAt.Should().Be(WatchlistFeatureTestFixture.Now);
        user.LastSeenAt.Should().Be(WatchlistFeatureTestFixture.Now);
        fixture.Context.Users.Should().ContainSingle();
    }
}
