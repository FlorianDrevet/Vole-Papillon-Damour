using FluentAssertions;
using NSubstitute;
using Vole_Papillon_Damour.Application.Books.Queries.Admin;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.tests.Books.Commands.ScanBook;

namespace Vole_Papillon_Damour.Application.tests.Books.Queries.Admin;

public sealed class GetAdminBookFairsQueryHandlerTests
{
    [Fact]
    public async Task Handle_FiltersValueConvertedEventTypesWithProviderCompatiblePredicate()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var now = new DateTime(2026, 9, 11, 10, 0, 0, DateTimeKind.Utc);
        var fair = await fixture.AddFairAsync(
            new DateTimeOffset(now.AddDays(-1), TimeSpan.Zero),
            new DateTimeOffset(now.AddDays(1), TimeSpan.Zero),
            null,
            null);
        await fixture.AddOtherEventAsync();
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(now);
        var handler = new GetAdminBookFairsQueryHandler(fixture.Context, clock);

        var result = await handler.Handle(
            new GetAdminBookFairsQuery(IncludeCancelled: false, Page: 1, PageSize: 50),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Fairs.Should().ContainSingle().Which.Id.Should().Be(fair.Id.Value);
    }

    [Fact]
    public async Task FairStats_FiltersValueConvertedPreviousEventTypesWithProviderCompatiblePredicate()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var now = new DateTime(2026, 9, 11, 10, 0, 0, DateTimeKind.Utc);
        var previousFair = await fixture.AddFairAsync(
            new DateTimeOffset(now.AddDays(-4), TimeSpan.Zero),
            new DateTimeOffset(now.AddDays(-3), TimeSpan.Zero),
            null,
            null);
        await fixture.AddOtherEventAsync();
        var currentFair = await fixture.AddFairAsync(
            new DateTimeOffset(now.AddDays(-1), TimeSpan.Zero),
            new DateTimeOffset(now, TimeSpan.Zero),
            null,
            null);
        var handler = new GetAdminFairStatsQueryHandler(fixture.Context);

        var result = await handler.Handle(
            new GetAdminFairStatsQuery(currentFair.Id.Value),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.PreviousFairs.Should().ContainSingle().Which.FairId.Should().Be(previousFair.Id.Value);
    }
}
