using System.Text;
using FluentAssertions;
using NSubstitute;
using Vole_Papillon_Damour.Application.Books.Queries.Admin;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.tests.Books.Commands.ScanBook;

namespace Vole_Papillon_Damour.Application.tests.Books.Queries.Admin;

public sealed class ExportIsbnNotFoundQueryHandlerTests
{
    private static readonly DateTime Now = new(2026, 9, 25, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Handle_ExportsEveryNotFoundIsbnInTheExpectedExcelCsvFormat()
    {
        await using var fixture = await ScanBookFixture.CreateAsync();
        var first = await fixture.AddBookAsync("9782070363735", quantityAvailable: 0);
        first.RecordMetadataNotFound(Now.AddDays(-2));
        var second = await fixture.AddBookAsync("9782070409228", quantityAvailable: 0);
        second.RecordMetadataNotFound(Now.AddDays(-1));
        var failed = await fixture.AddBookAsync("9782070612758", quantityAvailable: 0);
        failed.RecordMetadataProviderFailure(Now, "Fournisseur indisponible.");
        var discarded = await fixture.AddBookAsync("9782253004226", quantityAvailable: 0);
        discarded.RecordMetadataNotFound(Now);
        discarded.DiscardMetadataCandidate(Now);
        await fixture.AddBookAsync("9782253006329", quantityAvailable: 0);
        await fixture.Context.SaveChangesAsync();

        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(Now);
        var handler = new ExportIsbnNotFoundQueryHandler(fixture.Context, clock);

        var response = await handler.Handle(new ExportIsbnNotFoundQuery(), CancellationToken.None);

        response.IsError.Should().BeFalse();
        response.Value.FileName.Should().Be("isbn-non-trouves-2026-09-25.csv");
        var bytes = response.Value.Content;
        bytes.Take(3).Should().Equal(0xEF, 0xBB, 0xBF);

        var csv = Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF');
        var rows = csv.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries);
        rows.Should().HaveCount(3);
        rows[0].Should().Be(
            "ISBN;Titre;Auteurs;Editeur;Annee_publication;Format;Langue;Genre_principal;Genres;Resume;Pages;Edition;Couverture_URL");
        rows[1].Split(';').Should().Equal(first.Id.Value, "", "", "", "", "", "", "", "", "", "", "", "");
        rows[2].Split(';').Should().Equal(second.Id.Value, "", "", "", "", "", "", "", "", "", "", "", "");
    }
}
