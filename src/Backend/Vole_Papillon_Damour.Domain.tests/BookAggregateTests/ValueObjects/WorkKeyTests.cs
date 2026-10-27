using FluentAssertions;
using Vole_Papillon_Damour.Domain.BookAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Domain.tests.BookAggregateTests.ValueObjects;

public sealed class WorkKeyTests
{
    [Theory]
    [InlineData("Les misérables", "Hugo, Victor", "MISERABLES|HUGO")]
    [InlineData("Les Misérables", "Victor Hugo", "MISERABLES|HUGO")]
    [InlineData("Les Misérables ( Les Misérables, Tome 1)", "Victor Hugo", "MISERABLES|HUGO")]
    [InlineData("Les Misérables - Tome 2", "Victor Hugo", "MISERABLES|HUGO#2")]
    [InlineData("Germinal", "Zola, Émile", "GERMINAL|ZOLA")]
    [InlineData("Œuvres complètes", "Rimbaud", null)]
    [InlineData("Le petit prince", "Saint-Exupéry, Antoine de", "PETIT PRINCE|SAINT EXUPERY")]
    [InlineData("Astérix T. 3", "Goscinny", "ASTERIX|GOSCINNY#3")]
    [InlineData("Ça", "King", null)]
    [InlineData("Dune", "", null)]
    public void Compute_NormalizesTitleAuthorAndVolume(string? title, string? authors, string? expected)
    {
        var result = WorkKey.Compute(title, authors);

        result.Should().Be(expected);
    }

    [Fact]
    public void Compute_LongTitles_ReturnsDeterministicKeyWithinStorageLimit()
    {
        var title = new string('A', WorkKey.MaximumLength + 50);

        var result = WorkKey.Compute(title, "Hugo, Victor");

        result.Should().NotBeNull();
        result!.Length.Should().BeLessThanOrEqualTo(WorkKey.MaximumLength);
        result.Should().Be(WorkKey.Compute(title, "Hugo, Victor"));
        result.Should().NotBe(WorkKey.Compute(title + "B", "Hugo, Victor"));
    }

    [Fact]
    public void Book_RecomputesWorkKeyWhenTitleOrAuthorsChange()
    {
        Isbn13.TryCreate("9782070363735", out var isbn13).Should().BeTrue();
        var firstSeenAt = new DateTime(2026, 10, 23, 10, 0, 0, DateTimeKind.Utc);
        var book = Book.Create(isbn13, firstSeenAt);
        book.ApplyAutomaticMetadata(
            new BookMetadataPatch(
                "Les Misérables",
                "Hugo, Victor",
                null,
                null,
                null,
                null,
                null,
                null,
                [BookMetadataField.Title, BookMetadataField.Authors]),
            BookMetadataSource.Bnf,
            firstSeenAt.AddMinutes(1),
            rawPayload: null);

        book.WorkKey.Should().Be("MISERABLES|HUGO");

        book.ApplyManualMetadata(
            new BookMetadataPatch(
                null,
                "Alexandre Dumas",
                null,
                null,
                null,
                null,
                null,
                null,
                [BookMetadataField.Authors]),
            firstSeenAt.AddMinutes(2));

        book.WorkKey.Should().Be("MISERABLES|DUMAS");
    }
}
