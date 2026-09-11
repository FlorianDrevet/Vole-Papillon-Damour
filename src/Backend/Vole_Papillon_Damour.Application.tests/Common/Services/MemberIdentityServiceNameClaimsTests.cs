using FluentAssertions;
using NSubstitute;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.Common.Services;
using Vole_Papillon_Damour.Application.tests.WatchlistFeature;

namespace Vole_Papillon_Damour.Application.tests.Common.Services;

public sealed class MemberIdentityServiceNameClaimsTests
{
    [Fact]
    public async Task EnsureAsync_WhenSeparateNamesAreProvided_PreservesThemSeparately()
    {
        await using var fixture = await WatchlistFeatureTestFixture.CreateAsync();
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(WatchlistFeatureTestFixture.Now);
        var service = new MemberIdentityService(fixture.Context, clock);

        var user = await service.EnsureAsync(
            Guid.Parse("8d12cf6e-958f-4f68-a6f3-914d7bba8cf4"),
            "camille@example.test",
            "Camille",
            "De La Tour",
            CancellationToken.None);

        user.Name.Should().NotBeNull();
        user.Name!.FirstName.Should().Be("Camille");
        user.Name.LastName.Should().Be("De La Tour");
    }

    [Fact]
    public async Task EnsureAsync_WhenSeparateNamesAreProvided_SynchronizesEntraDisplayName()
    {
        await using var fixture = await WatchlistFeatureTestFixture.CreateAsync();
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(WatchlistFeatureTestFixture.Now);
        var directory = Substitute.For<IEntraUserDirectory>();
        var service = new MemberIdentityService(fixture.Context, clock, directory);

        await service.EnsureAsync(
            Guid.Parse("8d12cf6e-958f-4f68-a6f3-914d7bba8cf4"),
            "camille@example.test",
            "Camille",
            "De La Tour",
            CancellationToken.None);

        await directory.Received(1).UpdateDisplayNameAsync(
            "8d12cf6e-958f-4f68-a6f3-914d7bba8cf4",
            "Camille De La Tour",
            CancellationToken.None);
    }

    [Fact]
    public async Task EnsureAsync_WhenGraphSynchronizationFails_KeepsTheLocalIdentityAvailable()
    {
        await using var fixture = await WatchlistFeatureTestFixture.CreateAsync();
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(WatchlistFeatureTestFixture.Now);
        var directory = Substitute.For<IEntraUserDirectory>();
        directory.UpdateDisplayNameAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("Graph unavailable.")));
        var service = new MemberIdentityService(fixture.Context, clock, directory);

        var action = () => service.EnsureAsync(
            Guid.Parse("8d12cf6e-958f-4f68-a6f3-914d7bba8cf4"),
            "camille@example.test",
            "Camille",
            "De La Tour",
            CancellationToken.None);

        await action.Should().NotThrowAsync();
        await action.Should().NotThrowAsync();
        var user = fixture.Context.Users.Single();
        user.Name.Should().NotBeNull();
        user.Name!.FirstName.Should().Be("Camille");
        await directory.Received(2).UpdateDisplayNameAsync(
            "8d12cf6e-958f-4f68-a6f3-914d7bba8cf4",
            "Camille De La Tour",
            CancellationToken.None);
    }
}
