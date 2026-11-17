using System.Globalization;
using System.Text;
using FluentAssertions;
using Vole_Papillon_Damour.Application.Books.Queries.Admin;
using Vole_Papillon_Damour.Application.tests.Books.Commands.ScanBook;

namespace Vole_Papillon_Damour.Application.tests.Books.Queries.Admin;

public sealed class PreviewIsbnNotFoundImportQueryHandlerTests
{
    private static readonly DateTime Now = new(2026, 9, 25, 12, 0, 0, DateTimeKind.Utc);
    private const string Header =
        "ISBN;Titre;Auteurs;Editeur;Annee_publication;Format;Langue;Genre_principal;Genres;Resume;Pages;Edition;Couverture_URL";

    [Fact]
    public async Task Handle_ValidatesRowsAndPreviewsReadyIgnoredAndRejectedBooksWithoutWriting()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var isbn10Book = await fixture.AddBookAsync("9780306406157", quantityAvailable: 0);
        isbn10Book.RecordMetadataNotFound(Now);
        var withoutAuthor = await fixture.AddBookAsync("9782070363735", quantityAvailable: 0);
        withoutAuthor.RecordMetadataNotFound(Now);
        var blankBook = await fixture.AddBookAsync("9782070409228", quantityAvailable: 0);
        blankBook.RecordMetadataNotFound(Now);
        await fixture.Context.SaveChangesAsync();

        const string header =
            "Titre;ISBN;Année_publication;Auteurs;Editeur;Format;Langue;Genre_principal;Genres;Resume;Pages;Edition;Couverture_URL";
        var csv = string.Join("\r\n", header,
            "Livre de test;0-306-40615-2;1997;Auteur;Editeur;Poche;fr;Roman;Roman | Essai;Résumé;123;Édition;https://example.com/couverture.jpg",
            "Livre sans auteur;9782070363735;2020;;Editeur;Poche;fr;Roman;;;123;;https://example.com/couverture.jpg",
            ";9782070409228;;;;;;;;;;;",
            "Livre inconnu;9782070408504;2020;Auteur;Editeur;Poche;fr;Roman;;;123;;https://example.com/couverture.jpg");

        var content = Encoding.UTF8.GetBytes("\uFEFF" + csv);
        var handler = new PreviewIsbnNotFoundImportQueryHandler(fixture.Context);
        var response = await handler.Handle(
            new PreviewIsbnNotFoundImportQuery(content),
            CancellationToken.None);

        response.IsError.Should().BeFalse();
        response.Value.DataRowCount.Should().Be(4);
        response.Value.ReadyCount.Should().Be(2);
        response.Value.WithoutAuthorCount.Should().Be(1);
        response.Value.IgnoredCount.Should().Be(1);
        response.Value.RejectedCount.Should().Be(1);
        response.Value.ReadyIsbns.Should().BeEquivalentTo("9780306406157", "9782070363735");
        response.Value.Rejections.Should().ContainSingle()
            .Which.Reason.Should().Be("ISBN inconnu");
        isbn10Book.Title.Should().BeNull("preview must not write book metadata");
        withoutAuthor.Title.Should().BeNull("preview must not write book metadata");
        blankBook.MetadataStatus.ToString().Should().Be("NotFound");
    }

    [Fact]
    public async Task Handle_RejectsMissingAndUnknownHeadersWithBothColumnLists()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var invalidHeader = Header.Replace("ISBN;", "", StringComparison.Ordinal) + ";Inconnue";
        var handler = new PreviewIsbnNotFoundImportQueryHandler(fixture.Context);

        var result = await handler.Handle(
            new PreviewIsbnNotFoundImportQuery(Encoding.UTF8.GetBytes(invalidHeader)),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.Errors.Should().ContainSingle()
            .Which.Description.Should().Contain("colonnes manquantes : ISBN")
            .And.Contain("colonnes inconnues : Inconnue");
    }

    [Fact]
    public async Task Handle_ParsesQuotedSemicolonsAndEscapedQuotes()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var book = await fixture.AddBookAsync("9782070363735", quantityAvailable: 0);
        book.RecordMetadataNotFound(Now);
        await fixture.Context.SaveChangesAsync();
        var fields = new[]
        {
            "9782070363735", "\"Titre; illustré\"", "Auteur", "Editeur", "2020", "Poche", "fr",
            "Roman", "Roman|Essai", "\"Résumé avec \"\"guillemets\"\"\"", "123", "", ""
        };
        var handler = new PreviewIsbnNotFoundImportQueryHandler(fixture.Context);

        var result = await handler.Handle(
            new PreviewIsbnNotFoundImportQuery(Encoding.UTF8.GetBytes(Csv(string.Join(';', fields)))),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.ReadyCount.Should().Be(1);
        result.Value.RejectedCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_RejectsInvalidDuplicateUnknownAndAlreadyKnownBooksAndMissingTitle()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var notFound = await fixture.AddBookAsync("9780306406157", quantityAvailable: 0);
        notFound.RecordMetadataNotFound(Now);
        var missingTitle = await fixture.AddBookAsync("9782070363735", quantityAvailable: 0);
        missingTitle.RecordMetadataNotFound(Now);
        await fixture.AddBookAsync("9782070408504", quantityAvailable: 0);
        await fixture.Context.SaveChangesAsync();
        var handler = new PreviewIsbnNotFoundImportQueryHandler(fixture.Context);
        var csv = Csv(
            Row("not-an-isbn", "Livre invalide"),
            Row("9782070409228", "Livre inconnu"),
            Row("0-306-40615-2", "Première entrée"),
            Row("9780306406157", "Doublon"),
            Row("9782070408504", "Livre déjà présent"),
            Row("9782070363735", title: "", publisher: "Editeur"));

        var result = await handler.Handle(
            new PreviewIsbnNotFoundImportQuery(Encoding.UTF8.GetBytes(csv)),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.ReadyCount.Should().Be(1);
        result.Value.RejectedCount.Should().Be(5);
        result.Value.Rejections.Select(rejection => rejection.Reason).Should().Equal(
            "ISBN invalide",
            "ISBN inconnu",
            "ISBN en double dans le fichier",
            "Livre déjà doté d'une notice",
            "Titre manquant");
        notFound.Title.Should().BeNull("preview must not write metadata");
    }

    [Theory]
    [InlineData("title", 501, "Titre")]
    [InlineData("authors", 501, "Auteurs")]
    [InlineData("publisher", 201, "Editeur")]
    [InlineData("year", 0, "Annee_publication")]
    [InlineData("year", 10000, "Annee_publication")]
    [InlineData("format", 51, "Format")]
    [InlineData("language", 11, "Langue")]
    [InlineData("genre", 101, "Genre_principal")]
    [InlineData("genres", 101, "Genres")]
    [InlineData("summary", 20001, "Resume")]
    [InlineData("pages", 0, "Pages")]
    [InlineData("pages", 100001, "Pages")]
    [InlineData("edition", 101, "Edition")]
    [InlineData("cover", 1, "Couverture_URL")]
    public async Task Handle_RejectsValuesOutsideTheColumnLimits(string field, int value, string column)
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var book = await fixture.AddBookAsync("9782070363735", quantityAvailable: 0);
        book.RecordMetadataNotFound(Now);
        await fixture.Context.SaveChangesAsync();
        var invalidValue = field switch
        {
            "year" => value.ToString(CultureInfo.InvariantCulture),
            "pages" => value.ToString(CultureInfo.InvariantCulture),
            "cover" => "http://example.com/cover.jpg",
            "genres" => new string('g', value),
            _ => new string('x', value)
        };
        var csv = Csv(Row("9782070363735", "Titre valide", field, invalidValue));
        var handler = new PreviewIsbnNotFoundImportQueryHandler(fixture.Context);

        var result = await handler.Handle(
            new PreviewIsbnNotFoundImportQuery(Encoding.UTF8.GetBytes(csv)),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.RejectedCount.Should().Be(1);
        result.Value.Rejections.Single().Reason.Should().Contain(column);
    }

    [Fact]
    public async Task Handle_RejectsFilesAboveEitherSizeLimit()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var handler = new PreviewIsbnNotFoundImportQueryHandler(fixture.Context);
        var oversized = await handler.Handle(
            new PreviewIsbnNotFoundImportQuery(new byte[PreviewIsbnNotFoundImportQuery.MaximumFileSizeBytes + 1]),
            CancellationToken.None);
        var tooManyRows = await handler.Handle(
            new PreviewIsbnNotFoundImportQuery(Encoding.UTF8.GetBytes(Header + string.Concat(
                Enumerable.Repeat("\r\n", PreviewIsbnNotFoundImportQuery.MaximumDataRows + 2)))),
            CancellationToken.None);

        oversized.IsError.Should().BeTrue();
        oversized.Errors.Single().Description.Should().Contain("2 Mo");
        tooManyRows.IsError.Should().BeTrue();
        tooManyRows.Errors.Single().Description.Should().Contain("5 000 lignes");
    }

    [Fact]
    public async Task Handle_RejectsCoverUrlsAboveTheDomainLengthLimit()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var book = await fixture.AddBookAsync("9782070363735", quantityAvailable: 0);
        book.RecordMetadataNotFound(Now);
        await fixture.Context.SaveChangesAsync();
        var longUrl = "https://example.com/" + new string('x', 2_050);
        var csv = Csv(Row("9782070363735", "Titre valide", "cover-length", longUrl));

        var result = await new PreviewIsbnNotFoundImportQueryHandler(fixture.Context).Handle(
            new PreviewIsbnNotFoundImportQuery(Encoding.UTF8.GetBytes(csv)),
            CancellationToken.None);

        result.Value.Rejections.Single().Reason.Should().Contain("Couverture_URL");
    }

    private static string Csv(params string[] rows) => string.Join("\r\n", new[] { Header }.Concat(rows));

    private static string Row(
        string isbn,
        string title,
        string? field = null,
        string? value = null,
        string? publisher = null)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["ISBN"] = isbn,
            ["Titre"] = title,
            ["Editeur"] = publisher ?? string.Empty
        };
        if (field is not null && value is not null)
        {
            var column = field switch
            {
                "title" => "Titre",
                "authors" => "Auteurs",
                "publisher" => "Editeur",
                "year" => "Annee_publication",
                "format" => "Format",
                "language" => "Langue",
                "genre" => "Genre_principal",
                "genres" => "Genres",
                "summary" => "Resume",
                "pages" => "Pages",
                "edition" => "Edition",
                "cover" => "Couverture_URL",
                "cover-length" => "Couverture_URL",
                _ => throw new ArgumentOutOfRangeException(nameof(field), field, null)
            };
            values[column] = value;
        }

        return string.Join(';', Header.Split(';').Select(column => values.GetValueOrDefault(column, string.Empty)));
    }

}
