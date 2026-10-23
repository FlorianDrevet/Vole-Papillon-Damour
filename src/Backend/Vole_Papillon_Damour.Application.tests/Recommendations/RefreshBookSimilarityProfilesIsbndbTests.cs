using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using System.Text.Json;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.Recommendations.Commands.RecomputeBookNeighbors;
using Vole_Papillon_Damour.Application.Recommendations.Commands.RefreshBookSimilarityProfiles;
using Vole_Papillon_Damour.Application.Recommendations.Similarity;
using Vole_Papillon_Damour.Domain.BookAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate.Entities;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Infrastructure.Persistence.Configurations;

namespace Vole_Papillon_Damour.Application.tests.Recommendations;

public sealed class RefreshBookSimilarityProfilesIsbndbTests
{
    private static readonly DateTime Now = new(2026, 9, 25, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Handle_WhenFreeReaderAddsMetadata_DoesNotReplaceIsbndbSummaryOrSource()
    {
        await using var fixture = await RecommendationProfileFixture.CreateAsync();
        Isbn13.TryCreate("9782070363735", out var isbn13).Should().BeTrue();
        var book = Book.Create(isbn13, Now.AddDays(-1));
        book.ApplyIsbndbNotice(
            new BookMetadataPatch(
                "Le Petit Prince",
                "Antoine de Saint-Exupéry",
                "Gallimard",
                1943,
                null,
                null,
                null,
                null,
                [BookMetadataField.Title, BookMetadataField.Authors, BookMetadataField.Publisher,
                    BookMetadataField.PublicationYear]),
            Now.AddDays(-1));
        fixture.Context.Books.Add(book);
        fixture.Context.BookIsbndbRecords.Add(BookIsbndbRecord.Create(isbn13, Now.AddDays(-1)));
        await fixture.Context.SaveChangesAsync();

        var freeNotice = new SimilarityEdition(
            isbn13.Value,
            "Titre BnF secondaire",
            "Partie BnF",
            2,
            null,
            [],
            [],
            null,
            "Collection BnF",
            "Série BnF",
            3,
            "bnf:work-123",
            "/works/OL123W",
            [],
            [],
            "Adolescents",
            true,
            null,
            "Résumé BnF qui ne doit pas modifier la fiche ISBNdb parce qu'elle a déjà sa source principale.",
            "Description Open Library complémentaire suffisamment longue pour être conservée dans le profil.");
        var reader = new CapturingNoticeReader(_ => freeNotice);
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(Now);
        var handler = new RefreshBookSimilarityProfilesCommandHandler(
            fixture.Context,
            reader,
            CreateSettings(),
            clock);

        var result = await handler.Handle(new RefreshBookSimilarityProfilesCommand(), CancellationToken.None);

        result.Should().Be(new RefreshBookSimilarityProfilesResult(1, 1, 0));
        var persistedBook = await fixture.Context.Books.AsNoTracking().SingleAsync();
        persistedBook.MetadataSource.Should().Be(BookMetadataSource.Isbndb);
        persistedBook.Summary.Should().BeNull();
        var profile = await fixture.Context.BookSimilarityProfiles.SingleAsync();
        var edition = JsonSerializer.Deserialize<SimilarityEdition>(profile.NoticeJson!);
        edition.Should().NotBeNull();
        edition!.Title.Should().Be("Le Petit Prince");
        edition.Audience333.Should().Be("Adolescents");
        edition.CnljReviewed.Should().BeTrue();
        edition.Collection.Should().Be("Collection BnF");
        edition.SeriesTitle.Should().Be("Série BnF");
        edition.SeriesNumber.Should().Be(3);
        edition.PartNumber.Should().Be(2);
        edition.PartTitle.Should().Be("Partie BnF");
        edition.BnfWorkId.Should().Be("bnf:work-123");
        edition.OpenLibraryWorkKey.Should().Be("/works/OL123W");
        edition.OpenLibraryDescription.Should().StartWith("Description Open Library");
    }

    [Fact]
    public async Task Handle_WhenIsbndbBookHasNoFreeNotice_BuildsProfileFromStoredNoticeAndSynopsis()
    {
        await using var fixture = await RecommendationProfileFixture.CreateAsync();
        Isbn13.TryCreate("9782070409228", out var isbn13).Should().BeTrue();
        var book = Book.Create(isbn13, Now.AddDays(-1));
        book.ApplyIsbndbNotice(
            new BookMetadataPatch(
                "Les Misérables",
                "Victor Hugo",
                "Gallimard",
                1862,
                null,
                null,
                "Roman",
                null,
                [
                    BookMetadataField.Title,
                    BookMetadataField.Authors,
                    BookMetadataField.Publisher,
                    BookMetadataField.PublicationYear,
                    BookMetadataField.Genre,
                    BookMetadataField.Summary,
                    BookMetadataField.WorkId
                ],
                WorkId: "OL123W",
                Genres: ["Roman"],
                Summary: "Un synopsis ISBNdb distinctif pour ce test, assez long pour alimenter fidèlement le texte de recommandation et distinguer ce livre des autres titres."),
            Now.AddDays(-1));
        fixture.Context.Books.Add(book);
        fixture.Context.BookIsbndbRecords.Add(BookIsbndbRecord.Create(
            isbn13,
            Now.AddDays(-1),
            titleLong: "Les Misérables : récit intégral",
            authors: JsonSerializer.Serialize(new[] { "Hugo, Victor" }),
            subjects: JsonSerializer.Serialize(new[] { "Romans", "Robinsonades", "Self Service", "Victor Hugo" }),
            deweyDecimal: "843.7",
            synopsis: "Un synopsis ISBNdb distinctif pour ce test, assez long pour alimenter fidèlement le texte de recommandation et distinguer ce livre des autres titres."));
        await fixture.Context.SaveChangesAsync();
        (await fixture.Context.Books.SingleAsync()).Summary.Should().Be(
            "Un synopsis ISBNdb distinctif pour ce test, assez long pour alimenter fidèlement le texte de recommandation et distinguer ce livre des autres titres.");

        var reader = new CapturingNoticeReader(_ => null);
        var settings = CreateSettings();
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(Now);
        var refreshHandler = new RefreshBookSimilarityProfilesCommandHandler(
            fixture.Context,
            reader,
            settings,
            clock);

        var refreshResult = await refreshHandler.Handle(
            new RefreshBookSimilarityProfilesCommand(),
            CancellationToken.None);

        refreshResult.Should().Be(new RefreshBookSimilarityProfilesResult(1, 1, 0));
        reader.Calls.Should().Equal(isbn13.Value);
        (await fixture.Context.Books.AsNoTracking().SingleAsync()).Summary.Should().Contain("Un synopsis ISBNdb distinctif");
        var profile = await fixture.Context.BookSimilarityProfiles.SingleAsync();
        profile.NoticeFound.Should().BeTrue();
        var edition = JsonSerializer.Deserialize<SimilarityEdition>(profile.NoticeJson!);
        edition.Should().NotBeNull();
        edition!.Title.Should().Be("Les Misérables");
        edition.Subtitle.Should().Be("récit intégral");
        edition.Authors.Should().Contain("Victor Hugo");
        edition.AuthorSurnames.Should().Contain("Hugo");
        edition.OpenLibraryWorkKey.Should().Be("/works/OL123W");
        edition.Forms.Should().Contain("Roman");
        edition.Subjects.Should().Contain("Robinsonades");
        edition.Subjects.Should().NotContain("Self Service");
        edition.Subjects.Should().NotContain("Victor Hugo");
        edition.Dewey.Should().Be("843.7");

        var embeddings = Substitute.For<IBookEmbeddingService>();
        embeddings.IsConfigured.Returns(true);
        embeddings.EmbedAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult<IReadOnlyList<float[]>>(
                call.ArgAt<IReadOnlyList<string>>(0).Select(_ => new float[512]).ToArray()));
        var writer = Substitute.For<IBookNeighborWriter>();
        var recomputeHandler = new RecomputeBookNeighborsCommandHandler(
            fixture.Context,
            settings,
            embeddings,
            writer,
            clock);

        var recomputeResult = await recomputeHandler.Handle(
            new RecomputeBookNeighborsCommand(),
            CancellationToken.None);

        recomputeResult.Embedded.Should().Be(1);
        var persistedProfile = await fixture.Context.BookSimilarityProfiles.SingleAsync();
        persistedProfile.ProfileText.Should().Contain("Un synopsis ISBNdb distinctif pour ce test");
        persistedProfile.ProfileText.Should().Contain("Robinsonades");
        (await fixture.Context.Books.SingleAsync()).MetadataSource.Should().Be(BookMetadataSource.Isbndb);
    }

    private static IRecommendationSettings CreateSettings()
    {
        var settings = Substitute.For<IRecommendationSettings>();
        settings.Enabled.Returns(true);
        settings.ProfileBatchSize.Returns(20);
        settings.EmbeddingModelVersion.Returns("test-model");
        settings.NeighborsPerBook.Returns(10);
        return settings;
    }
}
