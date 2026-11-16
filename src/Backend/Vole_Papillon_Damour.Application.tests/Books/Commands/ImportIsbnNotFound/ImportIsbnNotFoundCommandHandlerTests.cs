using System.Text;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Vole_Papillon_Damour.Application.Books.Commands.ImportIsbnNotFound;
using Vole_Papillon_Damour.Application.Books.Queries.Admin;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.tests.Books.Commands.ScanBook;
using Vole_Papillon_Damour.Domain.BookAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.tests.Books.Commands.ImportIsbnNotFound;

public sealed class ImportIsbnNotFoundCommandHandlerTests
{
    private static readonly DateTime Now = new(2026, 9, 25, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid AdministratorId = Guid.Parse("00000000-0000-0000-0000-000000000021");
    private const string Header =
        "ISBN;Titre;Auteurs;Editeur;Annee_publication;Format;Langue;Genre_principal;Genres;Resume;Pages;Edition;Couverture_URL";

    [Fact]
    public async Task Handle_ImportsValidRowsRejectsOthersAndCompletesOnlyBooksWithAuthors()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var complete = await AddNotFoundAsync(fixture, "9780306406157");
        var withoutAuthor = await AddNotFoundAsync(fixture, "9782070363735");
        var csv = Csv(
            Row("9780306406157", "Livre importé", "Auteur", "Editeur", "2020", "Poche", "fr", "Roman",
                "Roman|Essai", "Résumé", "224", "Édition", "https://example.com/cover.jpg"),
            Row("9782070363735", "Livre sans auteur", "", "Editeur", "2021", "Poche", "fr", "Roman",
                "Roman", "Résumé", "123", "", ""),
            Row("9782070409228", "ISBN inconnu", "Auteur"));

        var handler = new ImportIsbnNotFoundCommandHandler(fixture.Context, Clock());
        var response = await handler.Handle(
            new ImportIsbnNotFoundCommand(
                Encoding.UTF8.GetBytes(csv),
                ["9780306406157", "9782070363735"],
                UserId.Create(AdministratorId)),
            CancellationToken.None);

        response.IsError.Should().BeFalse();
        response.Value.ImportedCount.Should().Be(2);
        response.Value.WithoutAuthorCount.Should().Be(1);
        response.Value.MarkedAbsentCount.Should().Be(2);
        response.Value.RejectedCount.Should().Be(1);
        response.Value.Rejections.Single().Reason.Should().Be("ISBN inconnu");

        Isbn13.TryCreate("9780306406157", out var completeIsbn).Should().BeTrue();
        var savedComplete = await fixture.Context.Books
            .Include(book => book.Genres)
            .SingleAsync(book => book.Id == completeIsbn);
        savedComplete.Title.Should().Be("Livre importé");
        savedComplete.Authors.Should().Be("Auteur");
        savedComplete.Publisher.Should().Be("Editeur");
        savedComplete.PublicationYear.Should().Be(2020);
        savedComplete.PhysicalFormat.Should().Be("Poche");
        savedComplete.Language.Should().Be("fr");
        savedComplete.Genre.Should().Be("Roman");
        savedComplete.Genres.Select(genre => genre.Name).Should().BeEquivalentTo("Roman", "Essai");
        savedComplete.Summary.Should().Be("Résumé");
        savedComplete.Pages.Should().Be(224);
        savedComplete.Edition.Should().Be("Édition");
        savedComplete.CoverUrl.Should().Be("https://example.com/cover.jpg");
        savedComplete.MetadataStatus.Should().Be(BookMetadataStatus.Manual);
        savedComplete.MetadataSource.Should().Be(BookMetadataSource.Manual);
        savedComplete.IsAbsentFromIsbndb.Should().BeTrue();
        savedComplete.IsbndbCompletedAt.Should().Be(Now);
        savedComplete.IsbndbCompletedBy!.Value.Should().Be(AdministratorId);

        var savedWithoutAuthor = await fixture.Context.Books.SingleAsync(book => book.Id == withoutAuthor.Id);
        savedWithoutAuthor.MetadataStatus.Should().Be(BookMetadataStatus.Manual);
        savedWithoutAuthor.IsAbsentFromIsbndb.Should().BeTrue();
        savedWithoutAuthor.IsbndbCompletedAt.Should().BeNull();
        savedWithoutAuthor.IsbndbCompletedBy.Should().BeNull();
    }

    [Fact]
    public async Task Handle_RejectsAValidBookThatChangedAfterItsPreview()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var book = await AddNotFoundAsync(fixture, "9782070363735");
        var csv = Csv(Row("9782070363735", "Titre de l'import", "Auteur"));
        var content = Encoding.UTF8.GetBytes(csv);
        var preview = await new PreviewIsbnNotFoundImportQueryHandler(fixture.Context).Handle(
            new PreviewIsbnNotFoundImportQuery(content),
            CancellationToken.None);
        preview.Value.ReadyIsbns.Should().Equal("9782070363735");

        book.ApplyAutomaticMetadata(
            new BookMetadataPatch(
                "Titre modifié ailleurs", null, null, null, null, null, null, null,
                [BookMetadataField.Title]),
            BookMetadataSource.Bnf,
            Now.AddMinutes(1),
            rawPayload: "{} ");
        await fixture.Context.SaveChangesAsync();

        var result = await new ImportIsbnNotFoundCommandHandler(fixture.Context, Clock()).Handle(
            new ImportIsbnNotFoundCommand(content, preview.Value.ReadyIsbns, UserId.Create(AdministratorId)),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.ImportedCount.Should().Be(0);
        result.Value.Rejections.Single().Reason.Should().Be("Livre modifié depuis l'aperçu");
        book.Title.Should().Be("Titre modifié ailleurs");
    }

    [Fact]
    public async Task Handle_ReimportingAnAlreadyImportedBookDoesNotChangeIt()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var book = await AddNotFoundAsync(fixture, "9782070363735");
        var content = Encoding.UTF8.GetBytes(Csv(Row("9782070363735", "Titre importé", "Auteur")));
        var previewHandler = new PreviewIsbnNotFoundImportQueryHandler(fixture.Context);
        var initialPreview = await previewHandler.Handle(
            new PreviewIsbnNotFoundImportQuery(content), CancellationToken.None);
        var importHandler = new ImportIsbnNotFoundCommandHandler(fixture.Context, Clock());
        var imported = await importHandler.Handle(
            new ImportIsbnNotFoundCommand(content, initialPreview.Value.ReadyIsbns, UserId.Create(AdministratorId)),
            CancellationToken.None);

        imported.Value.ImportedCount.Should().Be(1);
        var reimportPreview = await previewHandler.Handle(
            new PreviewIsbnNotFoundImportQuery(content), CancellationToken.None);
        reimportPreview.Value.ReadyIsbns.Should().BeEmpty();
        reimportPreview.Value.Rejections.Single().Reason.Should().Be("Livre déjà doté d'une notice");
        var reimported = await importHandler.Handle(
            new ImportIsbnNotFoundCommand(content, reimportPreview.Value.ReadyIsbns, UserId.Create(AdministratorId)),
            CancellationToken.None);

        reimported.Value.ImportedCount.Should().Be(0);
        reimported.Value.Rejections.Single().Reason.Should().Be("Livre déjà doté d'une notice");
        book.Title.Should().Be("Titre importé");
        book.IsbndbCompletedAt.Should().Be(Now);
    }

    private static async Task<Book> AddNotFoundAsync(
        ScanBookFixture fixture,
        string isbn)
    {
        var book = await fixture.AddBookAsync(isbn, quantityAvailable: 0);
        book.RecordMetadataNotFound(Now);
        await fixture.Context.SaveChangesAsync();
        return book;
    }

    private static string Csv(params string[] rows) => string.Join("\r\n", new[] { Header }.Concat(rows));

    private static string Row(
        string isbn,
        string title,
        string authors = "",
        string publisher = "",
        string year = "",
        string physicalFormat = "",
        string language = "",
        string genre = "",
        string genres = "",
        string summary = "",
        string pages = "",
        string edition = "",
        string coverUrl = "") =>
        string.Join(';', isbn, title, authors, publisher, year, physicalFormat, language, genre,
            genres, summary, pages, edition, coverUrl);

    private static IDateTimeProvider Clock()
    {
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(Now);
        return clock;
    }
}
