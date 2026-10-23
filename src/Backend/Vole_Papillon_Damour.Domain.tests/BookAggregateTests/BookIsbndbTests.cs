using FluentAssertions;
using Vole_Papillon_Damour.Domain.BookAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Domain.tests.BookAggregateTests;

public sealed class BookIsbndbTests
{
    private static readonly DateTime FirstSeenAt = new(2026, 10, 23, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ApplyIsbndbNotice_ReplacesFallbackAndPreservesManualCorrections()
    {
        var book = CreateBook();
        book.ApplyAutomaticMetadata(
            Patch(title: "Titre de secours", authors: "Auteur de secours", summary: "Résumé de secours"),
            BookMetadataSource.Bnf,
            FirstSeenAt.AddMinutes(1),
            "{\"source\":\"bnf\"}");
        book.ApplyManualMetadata(
            Patch(title: "Titre corrigé", fields: [BookMetadataField.Title]),
            FirstSeenAt.AddMinutes(2));

        book.ApplyIsbndbNotice(
            Patch(
                title: "Titre ISBNdb",
                authors: "Auteur ISBNdb",
                publisher: "Éditeur ISBNdb",
                summary: "Résumé ISBNdb",
                pages: 256,
                edition: "Deuxième édition",
                fields:
                [
                    BookMetadataField.Title,
                    BookMetadataField.Authors,
                    BookMetadataField.Publisher,
                    BookMetadataField.Summary,
                    BookMetadataField.Pages,
                    BookMetadataField.Edition
                ]),
            FirstSeenAt.AddMinutes(3));

        book.Title.Should().Be("Titre corrigé");
        book.Authors.Should().Be("Auteur ISBNdb");
        book.Publisher.Should().Be("Éditeur ISBNdb");
        book.Summary.Should().Be("Résumé ISBNdb");
        book.Pages.Should().Be(256);
        book.Edition.Should().Be("Deuxième édition");
        book.IsIsbndbSourced.Should().BeTrue();
        book.IsAbsentFromIsbndb.Should().BeFalse();
        book.IsDefinitiveForRecommendation.Should().BeTrue();
        book.MetadataStatus.Should().Be(BookMetadataStatus.Manual);
        book.MetadataSource.Should().Be(BookMetadataSource.Manual);
    }

    [Fact]
    public void ApplyIsbndbNotice_OnCompletedBookOnlyFillsEmptyFields()
    {
        var book = CreateBook();
        book.ApplyAutomaticMetadata(
            Patch(title: "Titre relu", authors: "Auteur relu"),
            BookMetadataSource.Bnf,
            FirstSeenAt.AddMinutes(1),
            rawPayload: null);
        book.MarkAbsentFromIsbndb(FirstSeenAt.AddMinutes(2));
        var admin = UserId.Create(Guid.Parse("12345678-1234-1234-1234-123456789012"));
        book.MarkIsbndbCompleted(admin, FirstSeenAt.AddMinutes(3));

        book.ApplyIsbndbNotice(
            Patch(
                title: "Titre ISBNdb",
                authors: "Auteur ISBNdb",
                summary: "Résumé complété par ISBNdb",
                pages: 144,
                fields:
                [
                    BookMetadataField.Title,
                    BookMetadataField.Authors,
                    BookMetadataField.Summary,
                    BookMetadataField.Pages
                ]),
            FirstSeenAt.AddMinutes(4));

        book.Title.Should().Be("Titre relu");
        book.Authors.Should().Be("Auteur relu");
        book.Summary.Should().Be("Résumé complété par ISBNdb");
        book.Pages.Should().Be(144);
        book.IsIsbndbSourced.Should().BeTrue();
        book.IsAbsentFromIsbndb.Should().BeFalse();
        book.IsbndbCompletedAt.Should().Be(FirstSeenAt.AddMinutes(3));
        book.IsbndbCompletedBy.Should().Be(admin);
    }

    [Fact]
    public void MarkAbsentFromIsbndb_PreservesExistingMetadataAndCanBeRechecked()
    {
        var book = CreateBook();
        book.ApplyAutomaticMetadata(
            Patch(title: "Titre de secours", authors: "Auteur"),
            BookMetadataSource.Bnf,
            FirstSeenAt.AddMinutes(1),
            rawPayload: null);
        var checkedAt = FirstSeenAt.AddMinutes(2);

        book.MarkAbsentFromIsbndb(checkedAt);
        book.RecordIsbndbCheck(checkedAt.AddDays(30));

        book.Title.Should().Be("Titre de secours");
        book.MetadataSource.Should().Be(BookMetadataSource.Bnf);
        book.IsIsbndbSourced.Should().BeFalse();
        book.IsAbsentFromIsbndb.Should().BeTrue();
        book.IsbndbCheckedAt.Should().Be(checkedAt.AddDays(30));
        book.IsDefinitiveForRecommendation.Should().BeTrue();
    }

    [Fact]
    public void MarkIsbndbCompleted_RequiresAbsentBookWithTitleAndAuthorAndCanBeReopened()
    {
        var book = CreateBook();
        var admin = UserId.Create(Guid.Parse("12345678-1234-1234-1234-123456789012"));
        var completedAt = FirstSeenAt.AddMinutes(2);

        var beforeAbsent = () => book.MarkIsbndbCompleted(admin, completedAt);
        beforeAbsent.Should().Throw<InvalidOperationException>();

        book.ApplyManualMetadata(Patch(title: "Titre", fields: [BookMetadataField.Title]), FirstSeenAt.AddMinutes(1));
        book.MarkAbsentFromIsbndb(FirstSeenAt.AddMinutes(1));
        var withoutAuthor = () => book.MarkIsbndbCompleted(admin, completedAt);
        withoutAuthor.Should().Throw<InvalidOperationException>();

        book.ApplyManualMetadata(Patch(authors: "Auteur", fields: [BookMetadataField.Authors]), FirstSeenAt.AddMinutes(1));
        book.MarkIsbndbCompleted(admin, completedAt).Should().BeTrue();
        book.MarkIsbndbCompleted(admin, completedAt.AddMinutes(1)).Should().BeFalse();
        book.ReopenIsbndbCompletion(completedAt.AddMinutes(2)).Should().BeTrue();
        book.IsbndbCompletedAt.Should().BeNull();
        book.IsbndbCompletedBy.Should().BeNull();
    }

    [Fact]
    public void ApplyIsbndbNotice_RejectsDiscardedBook()
    {
        var book = CreateBook();
        book.DiscardMetadataCandidate(FirstSeenAt.AddMinutes(1));

        var action = () => book.ApplyIsbndbNotice(Patch(title: "Titre"), FirstSeenAt.AddMinutes(2));

        action.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ApplyManualMetadata_StoresPagesAndEditionAndValidatesTheirRanges()
    {
        var book = CreateBook();
        book.ApplyManualMetadata(
            Patch(pages: 192, edition: "Poche", fields: [BookMetadataField.Pages, BookMetadataField.Edition]),
            FirstSeenAt.AddMinutes(1));

        book.Pages.Should().Be(192);
        book.Edition.Should().Be("Poche");

        var invalidPages = () => CreateBook().ApplyManualMetadata(
            Patch(pages: 0, fields: [BookMetadataField.Pages]),
            FirstSeenAt.AddMinutes(1));
        invalidPages.Should().Throw<ArgumentOutOfRangeException>();

        var invalidEdition = () => CreateBook().ApplyManualMetadata(
            Patch(edition: new string('x', 101), fields: [BookMetadataField.Edition]),
            FirstSeenAt.AddMinutes(1));
        invalidEdition.Should().Throw<ArgumentException>();
    }

    private static Book CreateBook()
    {
        Isbn13.TryCreate("9782070363735", out var isbn13).Should().BeTrue();
        return Book.Create(isbn13, FirstSeenAt);
    }

    private static BookMetadataPatch Patch(
        string? title = null,
        string? authors = null,
        string? publisher = null,
        string? summary = null,
        int? pages = null,
        string? edition = null,
        IReadOnlyCollection<BookMetadataField>? fields = null)
    {
        var selectedFields = fields?.ToArray();
        if (selectedFields is null)
        {
            var inferredFields = new List<BookMetadataField>();
            if (title is not null) inferredFields.Add(BookMetadataField.Title);
            if (authors is not null) inferredFields.Add(BookMetadataField.Authors);
            if (publisher is not null) inferredFields.Add(BookMetadataField.Publisher);
            if (summary is not null) inferredFields.Add(BookMetadataField.Summary);
            if (pages is not null) inferredFields.Add(BookMetadataField.Pages);
            if (edition is not null) inferredFields.Add(BookMetadataField.Edition);
            selectedFields = inferredFields.Count == 0 ? [BookMetadataField.Title] : inferredFields.ToArray();
        }

        return new BookMetadataPatch(
            title,
            authors,
            publisher,
            PublicationYear: null,
            PhysicalFormat: null,
            Language: null,
            Genre: null,
            CoverUrl: null,
            Fields: selectedFields,
            Summary: summary,
            Pages: pages,
            Edition: edition);
    }
}
