using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Domain.BookAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Books.Commands.Admin;

public sealed class ImportAdminBooksCommandHandler(
    IProjectDbContext dbContext,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<ImportAdminBooksCommand, ErrorOr<AdminBooksImportResult>>
{
    private const int MaximumBatchSize = 100;

    public async Task<ErrorOr<AdminBooksImportResult>> Handle(
        ImportAdminBooksCommand command,
        CancellationToken cancellationToken)
    {
        if (command.Isbns is null || command.Isbns.Count == 0)
        {
            return Error.Validation(
                "Book.InvalidImportBatch",
                "At least one ISBN-13 is required for an import batch.");
        }

        if (command.Isbns.Count > MaximumBatchSize)
        {
            return Error.Validation(
                "Book.ImportBatchTooLarge",
                $"An import batch cannot contain more than {MaximumBatchSize} ISBN-13 values.");
        }

        var requestedIsbns = new List<Isbn13>(command.Isbns.Count);
        foreach (var value in command.Isbns)
        {
            if (value is null ||
                value.Length != 13 ||
                !Isbn13.TryCreate(value, out var isbn13) ||
                !string.Equals(isbn13.Value, value, StringComparison.Ordinal))
            {
                return Error.Validation(
                    "Book.InvalidImportIsbn",
                    "Every imported value must be a canonical ISBN-13.");
            }

            requestedIsbns.Add(isbn13);
        }

        var utcNow = dateTimeProvider.UtcNow;
        if (utcNow.Kind != DateTimeKind.Utc)
        {
            return Error.Validation(
                "Book.InvalidImportTimestamp",
                "The import clock must return a UTC timestamp.");
        }

        var uniqueIsbns = requestedIsbns.Distinct().ToArray();
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable,
            cancellationToken);

        var existingIsbns = await dbContext.Books
            .Where(book => uniqueIsbns.Contains(book.Id))
            .Select(book => book.Id)
            .ToListAsync(cancellationToken);
        var knownIsbns = existingIsbns.ToHashSet();
        var results = new List<AdminBooksImportResult.Item>(requestedIsbns.Count);
        var createdCount = 0;
        var alreadyPresentCount = 0;

        foreach (var isbn13 in requestedIsbns)
        {
            if (!knownIsbns.Add(isbn13))
            {
                results.Add(new AdminBooksImportResult.Item(
                    isbn13.Value,
                    AdminBooksImportResult.Status.AlreadyPresent));
                alreadyPresentCount++;
                continue;
            }

            dbContext.Books.Add(Book.Create(isbn13, utcNow));
            results.Add(new AdminBooksImportResult.Item(
                isbn13.Value,
                AdminBooksImportResult.Status.Created));
            createdCount++;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new AdminBooksImportResult(
            requestedIsbns.Count,
            createdCount,
            alreadyPresentCount,
            results);
    }
}
