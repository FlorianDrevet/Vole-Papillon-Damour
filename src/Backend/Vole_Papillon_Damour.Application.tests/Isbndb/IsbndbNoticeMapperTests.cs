using FluentAssertions;
using Vole_Papillon_Damour.Application.Isbndb;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.tests.Isbndb;

public sealed class IsbndbNoticeMapperTests
{
    [Fact]
    public void ToPatch_MapsRecordedLesMiserablesNoticeAndIncludesOnlyAvailableFields()
    {
        var book = new IsbndbBook
        {
            Title = "Les Misérables",
            TitleLong = "Les Misérables, tome 1 : Jean Valjean",
            Isbn13 = "9782070409228",
            Isbn10 = "2070409226",
            Publisher = "Gallimard",
            Binding = "Paperback",
            Language = "fra",
            DatePublished = "1995-06-09",
            Edition = "Folio classique",
            Pages = 512,
            Image = "https://images.isbndb.com/covers/les-miserables.jpg",
            Synopsis = "<p>Un homme, Jean Valjean, cherche sa place dans la société &amp; protège Cosette.</p>",
            Authors = ["Hugo, Victor", "Saint-Exupéry, Antoine de"],
            Subjects = ["Romans", "Histoire"],
            OtherIsbns = [new IsbndbOtherIsbn { Isbn = "9782070612758", Binding = "Paperback" }],
            RawJson = "{\"title\":\"Les Misérables\"}"
        };

        var patch = IsbndbNoticeMapper.ToPatch(book);

        patch.Title.Should().Be("Les Misérables");
        patch.Authors.Should().Be("Victor Hugo, Antoine de Saint-Exupéry");
        patch.Publisher.Should().Be("Gallimard");
        patch.PublicationYear.Should().Be(1995);
        patch.PhysicalFormat.Should().Be("Broché");
        patch.Language.Should().Be("fr");
        patch.Summary.Should().Be("Un homme, Jean Valjean, cherche sa place dans la société & protège Cosette.");
        patch.CoverUrl.Should().Be("https://images.isbndb.com/covers/les-miserables.jpg");
        patch.Pages.Should().Be(512);
        patch.Edition.Should().Be("Folio classique");
        patch.Genre.Should().Be("Histoire");
        patch.Genres.Should().Equal("Histoire");
        patch.Fields.Should().Contain(new[]
        {
            BookMetadataField.Title,
            BookMetadataField.Authors,
            BookMetadataField.Publisher,
            BookMetadataField.PublicationYear,
            BookMetadataField.PhysicalFormat,
            BookMetadataField.Language,
            BookMetadataField.Summary,
            BookMetadataField.CoverUrl,
            BookMetadataField.Pages,
            BookMetadataField.Edition,
            BookMetadataField.Genre
        });
        patch.Fields.Should().HaveCount(11);
    }

    [Fact]
    public void ToPatch_UsesTitleLongAndOverviewWhenShorterNoticeFieldsAreMissing()
    {
        var patch = IsbndbNoticeMapper.ToPatch(new IsbndbBook
        {
            TitleLong = "Titre long",
            Overview = "<div>Résumé &eacute;tendu</div>",
            Binding = "Mass Market Paperback",
            Language = "deu",
            DatePublished = "2021-12",
            Image = "http://images.isbndb.com/temporary.jpg",
            Subjects = []
        });

        patch.Title.Should().Be("Titre long");
        patch.Summary.Should().Be("Résumé étendu");
        patch.PhysicalFormat.Should().Be("Poche");
        patch.Language.Should().Be("de");
        patch.PublicationYear.Should().Be(2021);
        patch.CoverUrl.Should().BeNull();
        patch.Fields.Should().NotContain(BookMetadataField.CoverUrl);
        patch.Fields.Should().Contain(BookMetadataField.Genre);
        patch.Genres.Should().BeEmpty();
    }

    [Fact]
    public void ToPatch_OmitsMissingOrInvalidMetadataFields()
    {
        var patch = IsbndbNoticeMapper.ToPatch(new IsbndbBook
        {
            Title = "Titre présent",
            Language = "an-invalid-language-code",
            DatePublished = "not a year",
            Pages = 0,
            Edition = new string('e', 130),
            Binding = new string('f', 60),
            Image = "javascript:alert(1)"
        });

        patch.Fields.Should().Equal(
            BookMetadataField.Title,
            BookMetadataField.PhysicalFormat,
            BookMetadataField.Edition);
        patch.Language.Should().BeNull();
        patch.PublicationYear.Should().BeNull();
        patch.Pages.Should().BeNull();
        patch.Edition.Should().HaveLength(100);
        patch.PhysicalFormat.Should().HaveLength(50);
        patch.CoverUrl.Should().BeNull();
        patch.Genres.Should().BeNull();
    }

    [Fact]
    public void ToRecord_PreservesTheRawIsbndbFieldsAndStructuredDetails()
    {
        Isbn13.TryCreate("9782070409228", out var isbn13).Should().BeTrue();
        using var structured = System.Text.Json.JsonDocument.Parse("""
            {"height":{"value":18.5,"unit":"cm"},"weight":{"value":350,"unit":"g"}}
            """);
        using var related = System.Text.Json.JsonDocument.Parse("""{"work":"les-miserables"}""");
        var fetchedAt = new DateTime(2026, 10, 23, 12, 0, 0, DateTimeKind.Utc);
        var book = new IsbndbBook
        {
            Isbn13 = isbn13.Value,
            TitleLong = "Les Misérables, tome 1",
            Isbn10 = "2070409226",
            Publisher = "Gallimard",
            Binding = "Paperback",
            DatePublished = "1995",
            Edition = "Folio classique",
            EditionNumber = 2,
            Dimensions = "18 x 11 cm",
            DimensionsStructured = structured.RootElement.Clone(),
            Msrp = 12.50m,
            ListPrice = new IsbndbPrice { Amount = 10.25m, Currency = "EUR" },
            Synopsis = "Résumé.",
            Overview = "Aperçu.",
            Excerpt = "Extrait.",
            DeweyDecimal = "843.7",
            Authors = ["Hugo, Victor"],
            Subjects = ["Romans", "Histoire"],
            OtherIsbns = [new IsbndbOtherIsbn { Isbn = "9782070612758", Binding = "Paperback" }],
            Related = related.RootElement.Clone(),
            Image = "https://images.isbndb.com/covers/les-miserables.jpg",
            RawJson = "{\"title\":\"Les Misérables\"}"
        };

        var record = IsbndbNoticeMapper.ToRecord(book, fetchedAt);

        record.Isbn13.Should().Be(isbn13);
        record.TitleLong.Should().Be("Les Misérables, tome 1");
        record.HeightValue.Should().Be(18.5m);
        record.HeightUnit.Should().Be("cm");
        record.WeightValue.Should().Be(350m);
        record.WeightUnit.Should().Be("g");
        record.Msrp.Should().Be(12.50m);
        record.ListPriceAmount.Should().Be(10.25m);
        record.ListPriceCurrency.Should().Be("EUR");
        record.Subjects.Should().Be("[\"Romans\",\"Histoire\"]");
        record.OtherIsbns.Should().Be("[{\"isbn\":\"9782070612758\",\"binding\":\"Paperback\"}]");
        record.Related.Should().Be("{\"work\":\"les-miserables\"}");
        record.RawJson.Should().Be(book.RawJson);
        record.FetchedAt.Should().Be(fetchedAt);
    }
}
