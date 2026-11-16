using System.Globalization;
using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Books.Queries.Admin;

public sealed class PreviewIsbnNotFoundImportQueryHandler(IProjectDbContext dbContext)
    : IRequestHandler<PreviewIsbnNotFoundImportQuery, ErrorOr<PreviewIsbnNotFoundImportResult>>
{
    public async Task<ErrorOr<PreviewIsbnNotFoundImportResult>> Handle(
        PreviewIsbnNotFoundImportQuery query,
        CancellationToken cancellationToken)
    {
        IsbnNotFoundCsvFile parsedFile;
        try
        {
            parsedFile = IsbnNotFoundCsv.Parse(query.FileContent);
        }
        catch (IsbnNotFoundCsvFormatException exception)
        {
            return Error.Validation("Book.InvalidIsbnNotFoundCsv", exception.Message);
        }

        var knownStatuses = await LoadBookStatusesAsync(parsedFile.Rows, cancellationToken);
        var seen = new HashSet<Isbn13>();
        var rejections = new List<IsbnNotFoundImportRejectionResult>();
        var readyCount = 0;
        var withoutAuthorCount = 0;
        var ignoredCount = 0;

        foreach (var row in parsedFile.Rows)
        {
            if (row.IsCompletelyEmpty)
            {
                ignoredCount++;
                continue;
            }

            if (!row.HasExpectedColumnCount)
            {
                Reject(row, row.Isbn, "La ligne ne contient pas 13 colonnes.");
                continue;
            }

            if (!Isbn13.TryCreate(row.Isbn, out var isbn13))
            {
                Reject(row, row.Isbn, "ISBN invalide");
                continue;
            }

            if (!seen.Add(isbn13))
            {
                Reject(row, isbn13.Value, "ISBN en double dans le fichier");
                continue;
            }

            if (!knownStatuses.TryGetValue(isbn13, out var status))
            {
                Reject(row, isbn13.Value, "ISBN inconnu");
                continue;
            }

            if (status != BookMetadataStatus.NotFound)
            {
                Reject(row, isbn13.Value, "Livre déjà doté d'une notice");
                continue;
            }

            if (row.HasNoBookData)
            {
                ignoredCount++;
                continue;
            }

            if (string.IsNullOrWhiteSpace(row.Title))
            {
                Reject(row, isbn13.Value, "Titre manquant");
                continue;
            }

            var fieldError = ValidateFields(row);
            if (fieldError is not null)
            {
                Reject(row, isbn13.Value, fieldError);
                continue;
            }

            readyCount++;
            if (string.IsNullOrWhiteSpace(row.Authors))
            {
                withoutAuthorCount++;
            }
        }

        return new PreviewIsbnNotFoundImportResult(
            parsedFile.Rows.Count,
            readyCount,
            withoutAuthorCount,
            ignoredCount,
            rejections.Count,
            rejections);

        void Reject(IsbnNotFoundCsvRow row, string? isbn, string reason) =>
            rejections.Add(new IsbnNotFoundImportRejectionResult(
                row.LineNumber,
                string.IsNullOrWhiteSpace(isbn) ? "—" : isbn.Trim(),
                reason));
    }

    private async Task<Dictionary<Isbn13, BookMetadataStatus>> LoadBookStatusesAsync(
        IReadOnlyList<IsbnNotFoundCsvRow> rows,
        CancellationToken cancellationToken)
    {
        var isbns = new HashSet<Isbn13>();
        foreach (var row in rows)
        {
            if (!row.IsCompletelyEmpty &&
                row.HasExpectedColumnCount &&
                Isbn13.TryCreate(row.Isbn, out var isbn13))
            {
                isbns.Add(isbn13);
            }
        }

        var statuses = new Dictionary<Isbn13, BookMetadataStatus>();
        foreach (var batch in isbns.Chunk(1_000))
        {
            var books = await dbContext.Books
                .AsNoTracking()
                .Where(book => batch.Contains(book.Id))
                .Select(book => new { book.Id, book.MetadataStatus })
                .ToListAsync(cancellationToken);

            foreach (var book in books)
            {
                statuses[book.Id] = book.MetadataStatus;
            }
        }

        return statuses;
    }

    private static string? ValidateFields(IsbnNotFoundCsvRow row)
    {
        if (row.Title.Length > 500) return "Titre : 500 caractères maximum.";
        if (row.Authors.Length > 500) return "Auteurs : 500 caractères maximum.";
        if (row.Publisher.Length > 200) return "Editeur : 200 caractères maximum.";
        if (row.PhysicalFormat.Length > 50) return "Format : 50 caractères maximum.";
        if (row.Language.Length > 10) return "Langue : 10 caractères maximum.";
        if (row.Genre.Length > 100) return "Genre_principal : 100 caractères maximum.";
        if (row.Summary.Length > 20_000) return "Resume : 20 000 caractères maximum.";
        if (row.Edition.Length > 100) return "Edition : 100 caractères maximum.";

        if (!string.IsNullOrWhiteSpace(row.PublicationYear) &&
            (!int.TryParse(row.PublicationYear, NumberStyles.None, CultureInfo.InvariantCulture, out var year) ||
             year is < 1 or > 9999))
        {
            return "Annee_publication : entier compris entre 1 et 9999 requis.";
        }

        if (!string.IsNullOrWhiteSpace(row.Pages) &&
            (!int.TryParse(row.Pages, NumberStyles.None, CultureInfo.InvariantCulture, out var pages) ||
             pages is < 1 or > 100_000))
        {
            return "Pages : entier compris entre 1 et 100 000 requis.";
        }

        if (!string.IsNullOrWhiteSpace(row.Genres))
        {
            foreach (var genre in row.Genres.Split('|').Select(value => value.Trim()))
            {
                if (genre.Length == 0) return "Genres : aucun genre ne peut être vide.";
                if (genre.Length > 100) return "Genres : 100 caractères maximum par genre.";
            }
        }

        if (!string.IsNullOrWhiteSpace(row.CoverUrl) &&
            (!Uri.TryCreate(row.CoverUrl, UriKind.Absolute, out var coverUri) ||
             coverUri.Scheme != Uri.UriSchemeHttps ||
             string.IsNullOrWhiteSpace(coverUri.Host)))
        {
            return "Couverture_URL : adresse HTTPS valide requise.";
        }

        return null;
    }
}
