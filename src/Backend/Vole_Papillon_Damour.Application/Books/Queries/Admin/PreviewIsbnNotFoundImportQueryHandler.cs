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

        var knownStatuses = await LoadBookStatusesAsync(dbContext, parsedFile.Rows, cancellationToken);
        var validation = IsbnNotFoundImportRowValidator.Validate(parsedFile.Rows, knownStatuses);
        var rejections = validation.Rejections
            .Select(rejection => new IsbnNotFoundImportRejectionResult(
                rejection.RowNumber,
                rejection.Isbn,
                rejection.Reason))
            .ToArray();

        return new PreviewIsbnNotFoundImportResult(
            validation.DataRowCount,
            validation.ReadyRows.Count,
            validation.WithoutAuthorCount,
            validation.IgnoredCount,
            rejections.Length,
            rejections,
            validation.ReadyRows.Select(ready => ready.Isbn13.Value).ToArray());
    }

    internal static async Task<Dictionary<BookCode, BookMetadataStatus>> LoadBookStatusesAsync(
        IProjectDbContext dbContext,
        IReadOnlyList<IsbnNotFoundCsvRow> rows,
        CancellationToken cancellationToken)
    {
        var isbns = IsbnNotFoundCsv.GetValidIsbns(rows);
        var statuses = new Dictionary<BookCode, BookMetadataStatus>();
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
}
