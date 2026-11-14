using FluentAssertions;
using Vole_Papillon_Damour.Application.FairDigest.Common;
using Vole_Papillon_Damour.Application.tests.Books.Queries;

namespace Vole_Papillon_Damour.Application.tests.FairDigest;

public sealed class FairDigestArrivalSelectorTests
{
    [Fact]
    public async Task ReadArrivedIsbns_UsesTheFirstEntryMovementAndRequiresItAfterThePreviousFair()
    {
        await using var fixture = await CatalogDeltaFixture.CreateAsync();
        await fixture.AddBookAsync("9782070363735", CatalogDeltaQueryTime().AddMinutes(-20));
        await fixture.AddBookAsync("9783140464079", CatalogDeltaQueryTime().AddMinutes(-20));
        await fixture.AddBookAsync("9780306406157", CatalogDeltaQueryTime().AddMinutes(-1));
        await fixture.AddBookEntryMovementAsync("9782070363735", CatalogDeltaQueryTime().AddMinutes(-10));
        await fixture.AddBookEntryMovementAsync("9783140464079", CatalogDeltaQueryTime().AddMinutes(-10));
        await fixture.AddBookEntryMovementAsync("9783140464079", CatalogDeltaQueryTime().AddMinutes(-1));
        await fixture.AddBookEntryMovementAsync("9780306406157", CatalogDeltaQueryTime().AddMinutes(-1));

        var arrived = await FairDigestArrivalSelector.ReadArrivedIsbnsAsync(
            fixture.Context,
            CatalogDeltaQueryTime().AddMinutes(-5),
            CancellationToken.None);

        arrived.Should().BeEquivalentTo("9780306406157");
    }

    private static DateTime CatalogDeltaQueryTime() =>
        new(2026, 9, 3, 17, 0, 0, DateTimeKind.Utc);
}
