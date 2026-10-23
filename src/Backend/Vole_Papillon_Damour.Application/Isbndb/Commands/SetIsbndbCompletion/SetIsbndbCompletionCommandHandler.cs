using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.Common.Errors;
using Vole_Papillon_Damour.Domain.RecommendationAggregate;

namespace Vole_Papillon_Damour.Application.Isbndb.Commands.SetIsbndbCompletion;

public sealed class SetIsbndbCompletionCommandHandler(
    IProjectDbContext dbContext,
    IDateTimeProvider dateTimeProvider,
    IBookRecommendationWorkQueue? recommendationWorkQueue = null)
    : IRequestHandler<SetIsbndbCompletionCommand, ErrorOr<SetIsbndbCompletionResult>>
{
    public async Task<ErrorOr<SetIsbndbCompletionResult>> Handle(
        SetIsbndbCompletionCommand command,
        CancellationToken cancellationToken)
    {
        if (!Isbn13.TryCreate(command.Isbn13, out var isbn13)) return Errors.Book.InvalidIsbn(command.Isbn13);
        var book = await dbContext.Books.SingleOrDefaultAsync(candidate => candidate.Id == isbn13, cancellationToken);
        if (book is null) return Errors.Book.NotFound(isbn13.Value);
        if (!book.IsAbsentFromIsbndb) return Errors.Book.NotFound(isbn13.Value);
        if (command.Completed && (string.IsNullOrWhiteSpace(book.Title) || string.IsNullOrWhiteSpace(book.Authors)))
            return Errors.Book.IsbndbCompletionIncomplete();
        if (command.Administrator is null || command.Administrator.Value == Guid.Empty)
            return Error.Validation("Book.InvalidUpdatedBy", "An updating user identifier is required.");

        var now = dateTimeProvider.UtcNow;
        var changed = command.Completed
            ? book.MarkIsbndbCompleted(command.Administrator, now)
            : book.ReopenIsbndbCompletion(now);
        BookRecommendationJob? job = null;
        if (changed && recommendationWorkQueue is not null)
        {
            job = await dbContext.BookRecommendationJobs.SingleOrDefaultAsync(
                candidate => candidate.Isbn13 == isbn13.Value, cancellationToken);
            if (job is not null && !job.Request(now, force: true)) job = null;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        if (job is not null)
            await recommendationWorkQueue!.EnqueueBooksAsync(
                [new BookRecommendationQueueItem(job.Isbn13, job.RequestVersion)], cancellationToken);

        return new SetIsbndbCompletionResult(
            isbn13.Value,
            book.IsbndbCompletedAt is not null,
            book.IsbndbCompletedAt is { } completedAt ? new DateTimeOffset(completedAt, TimeSpan.Zero) : null,
            book.IsbndbCompletedBy?.Value,
            changed);
    }
}
