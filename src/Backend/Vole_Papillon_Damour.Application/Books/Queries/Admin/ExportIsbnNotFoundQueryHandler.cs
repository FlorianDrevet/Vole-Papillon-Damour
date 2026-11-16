using System.Globalization;
using System.Text;
using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Books.Queries.Admin;

public sealed class ExportIsbnNotFoundQueryHandler(
    IProjectDbContext dbContext,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<ExportIsbnNotFoundQuery, ErrorOr<IsbnNotFoundCsvExportResult>>
{
    private const string Header =
        "ISBN;Titre;Auteurs;Editeur;Annee_publication;Format;Langue;Genre_principal;Genres;Resume;Pages;Edition;Couverture_URL";
    private static readonly Encoding Utf8WithBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);

    public async Task<ErrorOr<IsbnNotFoundCsvExportResult>> Handle(
        ExportIsbnNotFoundQuery query,
        CancellationToken cancellationToken)
    {
        var generatedAt = dateTimeProvider.UtcNow;
        if (generatedAt.Kind != DateTimeKind.Utc)
        {
            return Error.Validation("Book.InvalidClock", "The administration clock must be expressed in UTC.");
        }

        var isbns = await dbContext.Books
            .AsNoTracking()
            .Where(book => book.MetadataStatus == BookMetadataStatus.NotFound)
            .OrderBy(book => book.LastAttemptAt)
            .ThenBy(book => book.Id)
            .Select(book => book.Id.Value)
            .ToListAsync(cancellationToken);

        var csv = new StringBuilder(Header.Length + isbns.Count * 16)
            .AppendLine(Header);
        foreach (var isbn in isbns)
        {
            csv.Append(isbn).Append(';', 12).AppendLine();
        }

        var preamble = Utf8WithBom.GetPreamble();
        var content = Utf8WithBom.GetBytes(csv.ToString());
        var output = new byte[preamble.Length + content.Length];
        Buffer.BlockCopy(preamble, 0, output, 0, preamble.Length);
        Buffer.BlockCopy(content, 0, output, preamble.Length, content.Length);

        return new IsbnNotFoundCsvExportResult(
            output,
            $"isbn-non-trouves-{generatedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}.csv");
    }
}
