using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Domain.BookAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.BookMovementAggregate;
using Vole_Papillon_Damour.Domain.BookMovementAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Books.Commands.Admin;

public sealed class ImportAdminBooksCommandHandler(
    IProjectDbContext dbContext,
    IDateTimeProvider dateTimeProvider,
    IBookMetadataEnrichmentQueue metadataEnrichmentQueue)
    : IRequestHandler<ImportAdminBooksCommand, ErrorOr<AdminBooksImportResult>>
{
    private const int MaximumBatchSize = 100;
    private const string ImportMovementNote = "Import ISBN_application.csv";

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

        if (command.AddedBy is null || command.AddedBy.Value == Guid.Empty)
        {
            return Error.Validation(
                "Book.InvalidImportAddedBy",
                "An administrator identifier is required for inventory movements.");
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
        var createdIsbns = new List<Isbn13>(uniqueIsbns.Length);
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

            var book = Book.Create(isbn13, utcNow);
            book.RecordAvailableEntry(utcNow);
            dbContext.Books.Add(book);

            dbContext.BookMovements.Add(BookMovement.Create(
                BookMovementId.CreateUnique(),
                isbn13,
                BookMovementType.DirectEntry,
                quantity: 1,
                occurredAt: utcNow,
                receivedAt: utcNow,
                clockSuspect: false,
                scanSessionId: null,
                command.AddedBy,
                assoEventsId: null,
                ImportMovementNote,
                clientGestureId: null));
            createdIsbns.Add(isbn13);
            results.Add(new AdminBooksImportResult.Item(
                isbn13.Value,
                AdminBooksImportResult.Status.Created));
            createdCount++;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        foreach (var isbn13 in createdIsbns)
        {
            metadataEnrichmentQueue.Enqueue(isbn13);
        }

        return new AdminBooksImportResult(
            requestedIsbns.Count,
            createdCount,
            alreadyPresentCount,
            results);
    }
}
