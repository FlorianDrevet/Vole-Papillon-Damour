using FluentAssertions;
using Vole_Papillon_Damour.Application.Favorites.Common;
using Vole_Papillon_Damour.Application.tests.Books.Queries;

namespace Vole_Papillon_Damour.Application.tests.Favorites.Common;

public sealed class FavoriteCountReaderTests
{
    [Fact]
    public async Task GetCountsAsync_WhenOnlyOneEditionIsSeeded_IncludesFavoritesFromRelatedEditions()
    {
        await using var fixture = await CatalogDeltaFixture.CreateAsync();
        var updatedAt = GetCatalogDeltaQueryHandlerTests.GeneratedAt.AddMinutes(-1);
        var scannedEdition = await fixture.AddBookAsync(
            "9782070363735",
            updatedAt,
            workId: "OL123W");
        var favoriteEdition = await fixture.AddBookAsync(
            "9783140464079",
            updatedAt,
            workId: "OL123W");
        await fixture.AddMemberFavoriteAsync(
            Guid.Parse("00000000-0000-0000-0000-000000000010"),
            favoriteEdition.Id.Value);

        var counts = await FavoriteCountReader.GetCountsAsync(
            fixture.Context,
            [new FavoriteCountSeed(scannedEdition.Id, scannedEdition.WorkId)],
            CancellationToken.None);

        counts[scannedEdition.Id.Value].Should().Be(1);
    }
}
