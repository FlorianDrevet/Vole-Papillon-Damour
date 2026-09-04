using FluentAssertions;
using Vole_Papillon_Damour.Domain.BookAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Domain.tests.BookAggregateTests;

public sealed class BookMetadataTests
{
    private static readonly DateTime FirstSeenAt =
        new(2026, 9, 3, 17, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ApplyManualMetadata_UpdatesSelectedFieldsAndLocksThem()
    {
        var book = Book.Create(CreateIsbn("9782070363735"), FirstSeenAt);
        var patch = new BookMetadataPatch(
            Title: "Le Petit Prince",
            Authors: "Antoine de Saint-Exupéry",
            Publisher: null,
            PublicationYear: 1946,
            PhysicalFormat: null,
            Language: null,
            Genre: null,
            CoverBlobRef: null,
            Fields: [BookMetadataField.Title, BookMetadataField.Authors]);
        var updatedAt = FirstSeenAt.AddMinutes(2);

        var changed = book.ApplyManualMetadata(patch, updatedAt);

        changed.Should().BeTrue();
        book.Title.Should().Be("Le Petit Prince");
        book.Authors.Should().Be("Antoine de Saint-Exupéry");
        book.PublicationYear.Should().BeNull();
        book.MetadataStatus.Should().Be(BookMetadataStatus.Manual);
        book.MetadataSource.Should().Be(BookMetadataSource.Manual);
        book.ManuallyEditedFields.Should().Be("[\"Title\",\"Authors\"]");
        book.UpdatedAt.Should().Be(updatedAt);
    }

    [Fact]
    public void ApplyManualMetadata_MergesLocksAndAllowsClearingASelectedField()
    {
        var book = Book.Create(CreateIsbn("9782070363735"), FirstSeenAt);
        book.ApplyManualMetadata(
            new BookMetadataPatch(
                "Titre initial",
                "Auteur",
                null,
                null,
                null,
                null,
                null,
                null,
                [BookMetadataField.Title, BookMetadataField.Authors]),
            FirstSeenAt.AddMinutes(1));

        var changed = book.ApplyManualMetadata(
            new BookMetadataPatch(
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                [BookMetadataField.Title]),
            FirstSeenAt.AddMinutes(2));

        changed.Should().BeTrue();
        book.Title.Should().BeNull();
        book.Authors.Should().Be("Auteur");
        book.ManuallyEditedFields.Should().Be("[\"Title\",\"Authors\"]");
    }

    [Fact]
    public void ApplyManualMetadata_WithNoSelectedField_Throws()
    {
        var book = Book.Create(CreateIsbn("9782070363735"), FirstSeenAt);
        var patch = new BookMetadataPatch(
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            []);

        var action = () => book.ApplyManualMetadata(patch, FirstSeenAt.AddMinutes(1));

        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ApplyAutomaticMetadata_DoesNotOverwriteManuallyEditedFields()
    {
        var book = Book.Create(CreateIsbn("9782070363735"), FirstSeenAt);
        book.ApplyAutomaticMetadata(
            new BookMetadataPatch(
                "Titre source",
                "Auteur source",
                null,
                null,
                null,
                null,
                null,
                null,
                [BookMetadataField.Title, BookMetadataField.Authors]),
            BookMetadataSource.Bnf,
            FirstSeenAt.AddMinutes(1),
            "{\"source\":\"bnf\"}");
        book.ApplyManualMetadata(
            new BookMetadataPatch(
                "Titre corrigé",
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                [BookMetadataField.Title]),
            FirstSeenAt.AddMinutes(2));

        var changed = book.ApplyAutomaticMetadata(
            new BookMetadataPatch(
                "Titre automatique écrasant",
                "Auteur actualisé",
                null,
                null,
                null,
                null,
                null,
                null,
                [BookMetadataField.Title, BookMetadataField.Authors]),
            BookMetadataSource.OpenLibrary,
            FirstSeenAt.AddMinutes(3),
            "{\"source\":\"openlibrary\"}");

        changed.Should().BeTrue();
        book.Title.Should().Be("Titre corrigé");
        book.Authors.Should().Be("Auteur actualisé");
        book.MetadataStatus.Should().Be(BookMetadataStatus.Manual);
        book.MetadataSource.Should().Be(BookMetadataSource.Manual);
        book.MetadataFetchedAt.Should().Be(FirstSeenAt.AddMinutes(3));
        book.RawPayload.Should().Be("{\"source\":\"openlibrary\"}");
    }

    [Fact]
    public void ApplyAutomaticMetadata_StoresWorkIdentifierForWorkWatchlists()
    {
        var book = Book.Create(CreateIsbn("9782070363735"), FirstSeenAt);

        book.ApplyAutomaticMetadata(
            new BookMetadataPatch(
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                [BookMetadataField.WorkId],
                WorkId: "work-42"),
            BookMetadataSource.Bnf,
            FirstSeenAt.AddMinutes(1),
            null);

        book.WorkId.Should().Be("work-42");
    }

    [Fact]
    public void RecordMetadataNotFound_StoresNegativeCacheAndCountsTheAttempt()
    {
        var book = Book.Create(CreateIsbn("9782070363735"), FirstSeenAt);
        var attemptedAt = FirstSeenAt.AddMinutes(1);

        var changed = book.RecordMetadataNotFound(attemptedAt);

        changed.Should().BeTrue();
        book.MetadataStatus.Should().Be(BookMetadataStatus.NotFound);
        book.MetadataSource.Should().BeNull();
        book.ResolveAttempts.Should().Be(1);
        book.LastAttemptAt.Should().Be(attemptedAt);
        book.MetadataFetchedAt.Should().BeNull();
        book.UpdatedAt.Should().Be(attemptedAt);
    }

    [Fact]
    public void RecordMetadataNotFound_DoesNotChangeManualMetadataStatus()
    {
        var book = Book.Create(CreateIsbn("9782070363735"), FirstSeenAt);
        book.ApplyManualMetadata(
            new BookMetadataPatch(
                "Titre corrigé",
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                [BookMetadataField.Title]),
            FirstSeenAt.AddMinutes(1));

        book.RecordMetadataNotFound(FirstSeenAt.AddMinutes(2));

        book.MetadataStatus.Should().Be(BookMetadataStatus.Manual);
        book.MetadataSource.Should().Be(BookMetadataSource.Manual);
        book.Title.Should().Be("Titre corrigé");
        book.ResolveAttempts.Should().Be(1);
    }

    [Fact]
    public void RecordMetadataProviderFailure_RecordsCooldownWithoutConsumingNotFoundRetryBudget()
    {
        var book = Book.Create(CreateIsbn("9782070363735"), FirstSeenAt);
        book.RecordMetadataNotFound(FirstSeenAt.AddDays(-7));
        var failedAt = FirstSeenAt;

        var changed = book.RecordMetadataProviderFailure(failedAt);

        changed.Should().BeTrue();
        book.MetadataStatus.Should().Be(BookMetadataStatus.NotFound);
        book.ResolveAttempts.Should().Be(1);
        book.LastAttemptAt.Should().Be(failedAt);
        book.UpdatedAt.Should().Be(failedAt);
    }

    private static Isbn13 CreateIsbn(string value)
    {
        Isbn13.TryCreate(value, out var isbn).Should().BeTrue();
        return isbn;
    }
}
