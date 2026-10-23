using FluentAssertions;
using Vole_Papillon_Damour.Application.Isbndb;

namespace Vole_Papillon_Damour.Application.tests.Isbndb;

public sealed class IsbndbGenreMapperTests
{
    [Fact]
    public void Map_WhenSpecificFictionGenresArePresent_DoesNotAddGenericRoman()
    {
        var result = IsbndbGenreMapper.Map(
            ["Literature & Fiction", "Genre Fiction", "Science Fiction & Fantasy", "Fantasy", "Epic"],
            null,
            []);

        result.Genres.Should().Equal("Science-fiction", "Fantasy");
        result.UnmatchedSubjects.Should().BeEmpty();
    }

    [Fact]
    public void Map_IgnoresNoiseSubjectsAndSubjectsEqualToAuthors()
    {
        var noise = IsbndbGenreMapper.Map(
            ["Arborist Merchandising Root", "Self Service", "Subjects"],
            null,
            []);
        var author = IsbndbGenreMapper.Map(["Hugo, Victor"], null, ["Hugo, Victor"]);

        noise.Genres.Should().BeEmpty();
        noise.UnmatchedSubjects.Should().BeEmpty();
        author.Genres.Should().BeEmpty();
        author.UnmatchedSubjects.Should().BeEmpty();
    }

    [Theory]
    [InlineData("Bandes dessinées", "Bande dessinée")]
    [InlineData("Romans", "Roman")]
    [InlineData("Science-fiction", "Science-fiction")]
    [InlineData("Mémoires", "Biographie")]
    public void Map_MapsFrenchCatalogSubjects(string subject, string expectedGenre)
    {
        var result = IsbndbGenreMapper.Map([subject], null, []);

        result.Genres.Should().Equal(expectedGenre);
        result.UnmatchedSubjects.Should().BeEmpty();
    }

    [Theory]
    [InlineData("Literature & Fiction", "Roman")]
    [InlineData("Roman", "Roman")]
    [InlineData("Fiction", "Roman")]
    [InlineData("Literary", "Roman")]
    [InlineData("Classics", "Roman")]
    [InlineData("Genre Fiction", "Roman")]
    [InlineData("Science Fiction", "Science-fiction")]
    [InlineData("Fantasy", "Fantasy")]
    [InlineData("Comics", "Bande dessinée")]
    [InlineData("Graphic Novels", "Bande dessinée")]
    [InlineData("Comics & Graphic Novels", "Bande dessinée")]
    [InlineData("Manga", "Manga")]
    [InlineData("Mangas", "Manga")]
    [InlineData("Mystery", "Policier")]
    [InlineData("Policiers", "Policier")]
    [InlineData("Romans policiers", "Policier")]
    [InlineData("Crime", "Policier")]
    [InlineData("Detective", "Policier")]
    [InlineData("Thriller", "Thriller")]
    [InlineData("Thrillers", "Thriller")]
    [InlineData("Suspense", "Thriller")]
    [InlineData("Romance", "Romance")]
    [InlineData("Romans sentimentaux", "Romance")]
    [InlineData("History", "Histoire")]
    [InlineData("Histoire", "Histoire")]
    [InlineData("Historical", "Histoire")]
    [InlineData("Biography", "Biographie")]
    [InlineData("Biographies", "Biographie")]
    [InlineData("Biography & Autobiography", "Biographie")]
    [InlineData("Mémoires", "Biographie")]
    [InlineData("Juvenile Fiction", "Jeunesse")]
    [InlineData("Children's Books", "Jeunesse")]
    [InlineData("Jeunesse", "Jeunesse")]
    [InlineData("Albums jeunesse", "Jeunesse")]
    [InlineData("Young Adult", "Jeunesse")]
    [InlineData("Essays", "Essai")]
    [InlineData("Essais", "Essai")]
    [InlineData("Philosophy", "Essai")]
    [InlineData("Philosophie", "Essai")]
    [InlineData("Drama", "Théâtre")]
    [InlineData("Dramas & Plays", "Théâtre")]
    [InlineData("Théâtre", "Théâtre")]
    [InlineData("Poetry", "Poésie")]
    [InlineData("Poésie", "Poésie")]
    [InlineData("Cooking", "Cuisine")]
    [InlineData("Cookbooks", "Cuisine")]
    [InlineData("Cuisine", "Cuisine")]
    [InlineData("Reference", "Documentaire")]
    [InlineData("Study Aids", "Documentaire")]
    [InlineData("Education", "Documentaire")]
    [InlineData("Travel", "Documentaire")]
    [InlineData("Nature", "Documentaire")]
    [InlineData("Science", "Documentaire")]
    public void Map_MapsAllConfiguredExactSubjects(string subject, string expectedGenre)
    {
        var result = IsbndbGenreMapper.Map([subject], null, []);

        result.Genres.Should().Equal(expectedGenre);
        result.UnmatchedSubjects.Should().BeEmpty();
    }

    [Fact]
    public void Map_UsesPublisherListsSharedWithSimilarityFeatures()
    {
        var comic = IsbndbGenreMapper.Map([], "Éditions Dargaud", []);
        var manga = IsbndbGenreMapper.Map([], "Glénat Manga", []);

        comic.Genres.Should().Equal("Bande dessinée");
        manga.Genres.Should().Equal("Manga");
    }

    [Fact]
    public void Map_OnlyMapsEpicWhenFantasyIsAlsoPresent_AndReturnsOtherSubjectsForReporting()
    {
        var result = IsbndbGenreMapper.Map(["Epic", "Unusual topic"], null, []);

        result.Genres.Should().BeEmpty();
        result.UnmatchedSubjects.Should().Equal("Epic", "Unusual topic");
    }

    [Fact]
    public void Map_ReturnsAtMostThreeGenresInCatalogOrder()
    {
        var result = IsbndbGenreMapper.Map(
            ["Thriller", "Romance", "Histoire", "Cuisine", "Science Fiction"],
            null,
            []);

        result.Genres.Should().Equal("Science-fiction", "Thriller", "Romance");
    }
}
