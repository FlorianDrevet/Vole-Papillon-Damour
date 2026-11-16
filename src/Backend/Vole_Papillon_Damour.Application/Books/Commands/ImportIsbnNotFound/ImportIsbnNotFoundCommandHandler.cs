using System.Globalization;
using System.Data;
using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Domain.BookAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Books.Commands.ImportIsbnNotFound;

public sealed class ImportIsbnNotFoundCommandHandler(
    IProjectDbContext dbContext,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<ImportIsbnNotFoundCommand, ErrorOr<ImportIsbnNotFoundResult>>
{
    public async Task<ErrorOr<ImportIsbnNotFoundResult>> Handle(
        ImportIsbnNotFoundCommand command,
        CancellationToken cancellationToken)
    {
        if (command.ImportedBy is null || command.ImportedBy.Value == Guid.Empty)
        {
            return Error.Validation("Book.InvalidIsbnNotFoundImportUser", "An administrator identifier is required.");
        }

        if (command.ReadyIsbns is null)
        {
            return Error.Validation("Book.InvalidIsbnNotFoundPreview", "The preview ISBN list is required.");
        }

        var previewReadyIsbns = new HashSet<string>(StringComparer.Ordinal);
        foreach (var value in command.ReadyIsbns)
        {
            if (!Isbn13.TryCreate(value, out var isbn13) ||
                !string.Equals(value, isbn13.Value, StringComparison.Ordinal))
            {
                return Error.Validation("Book.InvalidIsbnNotFoundPreview", "The preview ISBN list is invalid.");
            }

            previewReadyIsbns.Add(isbn13.Value);
        }

        var updatedAt = dateTimeProvider.UtcNow;
        if (updatedAt.Kind != DateTimeKind.Utc)
        {
            return Error.Validation("Book.InvalidIsbnNotFoundImportTimestamp", "The import clock must return UTC.");
        }

        IsbnNotFoundCsvFile parsedFile;
        try
        {
            parsedFile = IsbnNotFoundCsv.Parse(command.FileContent);
        }
        catch (IsbnNotFoundCsvFormatException exception)
        {
            return Error.Validation("Book.InvalidIsbnNotFoundCsv", exception.Message);
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var booksByIsbn = await LoadBooksAsync(parsedFile.Rows, cancellationToken);
        var statuses = booksByIsbn.ToDictionary(pair => pair.Key, pair => pair.Value.MetadataStatus);
        var validation = IsbnNotFoundImportRowValidator.Validate(
            parsedFile.Rows,
            statuses,
            previewReadyIsbns);

        var markedAbsentCount = 0;
        foreach (var ready in validation.ReadyRows)
        {
            var book = booksByIsbn[ready.Isbn13];
            book.ApplyManualMetadata(ToMetadataPatch(ready.CsvRow), updatedAt);
            if (!book.IsAbsentFromIsbndb)
            {
                book.MarkAbsentFromIsbndb(updatedAt);
                markedAbsentCount++;
            }

            if (!string.IsNullOrWhiteSpace(book.Authors))
            {
                book.MarkIsbndbCompleted(command.ImportedBy, updatedAt);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var rejections = validation.Rejections
            .Select(rejection => new ImportIsbnNotFoundRejectionResult(
                rejection.RowNumber,
                rejection.Isbn,
                rejection.Reason))
            .ToArray();

        return new ImportIsbnNotFoundResult(
            validation.DataRowCount,
            validation.ReadyRows.Count,
            validation.WithoutAuthorCount,
            markedAbsentCount,
            rejections.Length,
            rejections);
    }

    private async Task<Dictionary<Isbn13, Book>> LoadBooksAsync(
        IReadOnlyList<IsbnNotFoundCsvRow> rows,
        CancellationToken cancellationToken)
    {
        var booksByIsbn = new Dictionary<Isbn13, Book>();
        foreach (var batch in IsbnNotFoundCsv.GetValidIsbns(rows).Chunk(1_000))
        {
            var books = await dbContext.Books
                .Include(book => book.Genres)
                .Where(book => batch.Contains(book.Id))
                .ToListAsync(cancellationToken);

            foreach (var book in books)
            {
                booksByIsbn[book.Id] = book;
            }
        }

        return booksByIsbn;
    }

    private static BookMetadataPatch ToMetadataPatch(IsbnNotFoundCsvRow row)
    {
        var fields = new HashSet<BookMetadataField> { BookMetadataField.Title };
        if (!string.IsNullOrWhiteSpace(row.Authors)) fields.Add(BookMetadataField.Authors);
        if (!string.IsNullOrWhiteSpace(row.Publisher)) fields.Add(BookMetadataField.Publisher);
        if (!string.IsNullOrWhiteSpace(row.PublicationYear)) fields.Add(BookMetadataField.PublicationYear);
        if (!string.IsNullOrWhiteSpace(row.PhysicalFormat)) fields.Add(BookMetadataField.PhysicalFormat);
        if (!string.IsNullOrWhiteSpace(row.Language)) fields.Add(BookMetadataField.Language);
        if (!string.IsNullOrWhiteSpace(row.Genre) || !string.IsNullOrWhiteSpace(row.Genres))
        {
            fields.Add(BookMetadataField.Genre);
        }

        if (!string.IsNullOrWhiteSpace(row.CoverUrl)) fields.Add(BookMetadataField.CoverUrl);
        if (!string.IsNullOrWhiteSpace(row.Summary)) fields.Add(BookMetadataField.Summary);
        if (!string.IsNullOrWhiteSpace(row.Pages)) fields.Add(BookMetadataField.Pages);
        if (!string.IsNullOrWhiteSpace(row.Edition)) fields.Add(BookMetadataField.Edition);

        var genres = string.IsNullOrWhiteSpace(row.Genres)
            ? null
            : row.Genres.Split('|').Select(value => value.Trim()).ToArray();

        return new BookMetadataPatch(
            row.Title,
            NullIfBlank(row.Authors),
            NullIfBlank(row.Publisher),
            ParseIntOrNull(row.PublicationYear),
            NullIfBlank(row.PhysicalFormat),
            NullIfBlank(row.Language),
            NullIfBlank(row.Genre),
            NullIfBlank(row.CoverUrl),
            fields,
            Genres: genres,
            Summary: NullIfBlank(row.Summary),
            Pages: ParseIntOrNull(row.Pages),
            Edition: NullIfBlank(row.Edition));
    }

    private static int? ParseIntOrNull(string value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : int.Parse(value, NumberStyles.None, CultureInfo.InvariantCulture);

    private static string? NullIfBlank(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
