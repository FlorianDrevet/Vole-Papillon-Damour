using System.Globalization;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Books.Common;

internal static class IsbnNotFoundImportRowValidator
{
    public static IsbnNotFoundImportValidation Validate(
        IReadOnlyList<IsbnNotFoundCsvRow> rows,
        IReadOnlyDictionary<Isbn13, BookMetadataStatus> knownStatuses,
        IReadOnlySet<string>? previewReadyIsbns = null)
    {
        var seen = new HashSet<Isbn13>();
        var readyRows = new List<IsbnNotFoundImportReadyRow>();
        var rejections = new List<IsbnNotFoundImportRejection>();
        var ignoredCount = 0;

        foreach (var row in rows)
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
                var reason = previewReadyIsbns?.Contains(isbn13.Value) == true
                    ? "Livre modifié depuis l'aperçu"
                    : "Livre déjà doté d'une notice";
                Reject(row, isbn13.Value, reason);
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

            if (previewReadyIsbns is not null && !previewReadyIsbns.Contains(isbn13.Value))
            {
                Reject(row, isbn13.Value, "Livre modifié depuis l'aperçu");
                continue;
            }

            readyRows.Add(new IsbnNotFoundImportReadyRow(row, isbn13));
        }

        return new IsbnNotFoundImportValidation(rows.Count, readyRows, ignoredCount, rejections);

        void Reject(IsbnNotFoundCsvRow row, string? isbn, string reason) =>
            rejections.Add(new IsbnNotFoundImportRejection(
                row.LineNumber,
                string.IsNullOrWhiteSpace(isbn) ? "—" : isbn.Trim(),
                reason));
    }

    public static string? ValidateFields(IsbnNotFoundCsvRow row)
    {
        if (row.Title.Length > 500) return "Titre : 500 caractères maximum.";
        if (row.Authors.Length > 500) return "Auteurs : 500 caractères maximum.";
        if (row.Publisher.Length > 200) return "Editeur : 200 caractères maximum.";
        if (row.PhysicalFormat.Length > 50) return "Format : 50 caractères maximum.";
        if (row.Language.Length > 10) return "Langue : 10 caractères maximum.";
        if (row.Genre.Length > 100) return "Genre_principal : 100 caractères maximum.";
        if (row.Summary.Length > 20_000) return "Resume : 20 000 caractères maximum.";
        if (row.Edition.Length > 100) return "Edition : 100 caractères maximum.";
        if (row.CoverUrl.Length > 2048) return "Couverture_URL : 2 048 caractères maximum.";

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

internal sealed record IsbnNotFoundImportReadyRow(IsbnNotFoundCsvRow CsvRow, Isbn13 Isbn13);

internal sealed record IsbnNotFoundImportRejection(int RowNumber, string Isbn, string Reason);

internal sealed record IsbnNotFoundImportValidation(
    int DataRowCount,
    IReadOnlyList<IsbnNotFoundImportReadyRow> ReadyRows,
    int IgnoredCount,
    IReadOnlyList<IsbnNotFoundImportRejection> Rejections)
{
    public int WithoutAuthorCount => ReadyRows.Count(ready => string.IsNullOrWhiteSpace(ready.CsvRow.Authors));
}
