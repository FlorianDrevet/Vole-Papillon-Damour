using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.Common.Errors;

namespace Vole_Papillon_Damour.Application.Books.Commands.Background;

public sealed class RetryBookMetadataCommandHandler(
    IProjectDbContext dbContext,
    IDateTimeProvider dateTimeProvider,
    IBookMetadataEnrichmentQueue enrichmentQueue)
    : IRequestHandler<RetryBookMetadataCommand, ErrorOr<RetryBookMetadataResult>>
{
    public async Task<ErrorOr<RetryBookMetadataResult>> Handle(
        RetryBookMetadataCommand command,
        CancellationToken cancellationToken)
    {
        if (!Isbn13.TryCreate(command.Isbn, out var isbn13))
        {
            return Errors.Book.InvalidIsbn(command.Isbn);
        }

        if (command.RequestedBy is null || command.RequestedBy.Value == Guid.Empty)
        {
            return Error.Validation("Book.InvalidUpdatedBy", "An updating user identifier is required.");
        }

        var retriedAt = dateTimeProvider.UtcNow;
        if (retriedAt.Kind != DateTimeKind.Utc)
        {
            return Errors.Book.InvalidScanTimestamp();
        }

        var book = await dbContext.Books
            .SingleOrDefaultAsync(candidate => candidate.Id == isbn13, cancellationToken);
        if (book is null)
        {
            return Errors.Book.NotFound(isbn13.Value);
        }

        if (book.MetadataStatus is BookMetadataStatus.Manual or BookMetadataStatus.Discarded ||
            !string.IsNullOrWhiteSpace(book.Title) &&
            book.MetadataStatus == BookMetadataStatus.Resolved)
        {
            return Error.Validation(
                "Book.MetadataRetryNotAvailable",
                "This book does not need automatic metadata enrichment.");
        }

        book.PrepareMetadataRetry(retriedAt);
        await dbContext.SaveChangesAsync(cancellationToken);
        enrichmentQueue.Enqueue(isbn13);

        return new RetryBookMetadataResult(
            isbn13.Value,
            book.MetadataStatus.ToString(),
            Queued: true);
    }
}
