using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.Common.Errors;
using Vole_Papillon_Damour.Domain.IsbndbAggregate;
using Vole_Papillon_Damour.Domain.RecommendationAggregate;

namespace Vole_Papillon_Damour.Application.Isbndb.Commands.RecheckIsbndbBook;

public sealed class RecheckIsbndbBookCommandHandler(
    IProjectDbContext dbContext,
    IIsbndbLookupService lookupService,
    IDateTimeProvider dateTimeProvider,
    IBookRecommendationWorkQueue? recommendationWorkQueue = null)
    : IRequestHandler<RecheckIsbndbBookCommand, ErrorOr<RecheckIsbndbBookResult>>
{
    public async Task<ErrorOr<RecheckIsbndbBookResult>> Handle(
        RecheckIsbndbBookCommand command,
        CancellationToken cancellationToken)
    {
        if (!Isbn13.TryCreate(command.Isbn13, out var isbn13)) return Errors.Book.InvalidIsbn(command.Isbn13);
        var book = await dbContext.Books.Include(candidate => candidate.Genres)
            .SingleOrDefaultAsync(candidate => candidate.Id == isbn13, cancellationToken);
        if (book is null) return Errors.Book.NotFound(isbn13.Value);
        if (book.IsbndbCompletedAt is not null)
            return Error.Conflict("Isbndb.BookCompleted", "A completed ISBNdb-absent book must not be overwritten.");

        var lookup = await lookupService.LookupAsync(isbn13, IsbndbUsage.Administration, cancellationToken);
        var now = dateTimeProvider.UtcNow;
        var queue = false;
        switch (lookup)
        {
            case IsbndbLookup.QuotaExhausted quota:
                var resetsAt = quota.ResetsAt ?? await dbContext.IsbndbQuotaStates.AsNoTracking()
                    .Where(state => state.Id == IsbndbQuotaState.SingletonId)
                    .Select(state => state.ExhaustedUntil ?? state.ResetsAt)
                    .SingleOrDefaultAsync(cancellationToken) ?? now.Date.AddDays(1);
                return new RecheckIsbndbBookResult(
                    isbn13.Value,
                    "QuotaExhausted",
                    new DateTimeOffset(resetsAt, TimeSpan.Zero));
            case IsbndbLookup.Unavailable:
                return new RecheckIsbndbBookResult(isbn13.Value, "Unavailable");
            case IsbndbLookup.NotFound:
                book.MarkAbsentFromIsbndb(now);
                queue = true;
                break;
            case IsbndbLookup.Found found:
                var patch = IsbndbNoticeMapper.ToPatch(found.Book);
                if (patch.Fields.Count == 0) return Error.Validation("Isbndb.EmptyNotice", "ISBNdb returned no usable book metadata.");
                book.ApplyIsbndbNotice(patch, now);
                var record = IsbndbNoticeMapper.ToRecord(found.Book, now);
                var existing = await dbContext.BookIsbndbRecords.SingleOrDefaultAsync(
                    candidate => candidate.Isbn13 == isbn13, cancellationToken);
                if (existing is null) dbContext.BookIsbndbRecords.Add(record);
                else dbContext.BookIsbndbRecords.Update(record);
                queue = true;
                break;
        }

        BookRecommendationJob? job = null;
        if (queue && recommendationWorkQueue is not null)
        {
            job = await dbContext.BookRecommendationJobs.SingleOrDefaultAsync(
                candidate => candidate.Isbn13 == isbn13.Value, cancellationToken);
            if (job is not null && !job.Request(now, force: true)) job = null;
        }
        await dbContext.SaveChangesAsync(cancellationToken);
        if (job is not null)
            await recommendationWorkQueue!.EnqueueBooksAsync(
                [new BookRecommendationQueueItem(job.Isbn13, job.RequestVersion)], cancellationToken);
        return new RecheckIsbndbBookResult(isbn13.Value, lookup is IsbndbLookup.Found ? "Found" : "NotFound");
    }
}
