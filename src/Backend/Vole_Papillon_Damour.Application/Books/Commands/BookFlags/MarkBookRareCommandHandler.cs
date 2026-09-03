using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.Common.Errors;

namespace Vole_Papillon_Damour.Application.Books.Commands.BookFlags;

public sealed class MarkBookRareCommandHandler(
    IProjectDbContext dbContext,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<MarkBookRareCommand, ErrorOr<BookFlagResult>>
{
    public async Task<ErrorOr<BookFlagResult>> Handle(
        MarkBookRareCommand command,
        CancellationToken cancellationToken)
    {
        if (!Isbn13.TryCreate(command.Isbn, out var isbn13))
        {
            return Errors.Book.InvalidIsbn(command.Isbn);
        }

        if (command.UpdatedBy is null || command.UpdatedBy.Value == Guid.Empty)
        {
            return Error.Validation("Book.InvalidUpdatedBy", "An updating user identifier is required.");
        }

        var updatedAt = dateTimeProvider.UtcNow;
        if (updatedAt.Kind != DateTimeKind.Utc)
        {
            return Errors.Book.InvalidScanTimestamp();
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var book = await dbContext.Books
            .SingleOrDefaultAsync(candidate => candidate.Id == isbn13, cancellationToken);
        if (book?.RedirectedToIsbn13 is { } canonicalIsbn13)
        {
            isbn13 = canonicalIsbn13;
            book = await dbContext.Books
                .SingleOrDefaultAsync(candidate => candidate.Id == isbn13, cancellationToken);
        }

        if (book is null)
        {
            return Errors.Book.NotFound(isbn13.Value);
        }

        var changed = book.UpdateRareStatus(command.IsRare, updatedAt);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new BookFlagResult(book.Isbn13.Value, book.IsRare, book.IsHiddenFromCatalog, changed);
    }
}
