using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.RecommendationAggregate;

namespace Vole_Papillon_Damour.Application.Recommendations.Commands.SeedRecommendationsBackfillPage;

public sealed class SeedRecommendationsBackfillPageCommandHandler(
    IProjectDbContext dbContext,
    IBookRecommendationWorkQueue workQueue,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<SeedRecommendationsBackfillPageCommand, SeedRecommendationsBackfillPageResult>
{
    private const int PageSize = 250;

    public async Task<SeedRecommendationsBackfillPageResult> Handle(
        SeedRecommendationsBackfillPageCommand request,
        CancellationToken cancellationToken)
    {
        if (request.BackfillId == Guid.Empty)
        {
            throw new ArgumentException("A backfill identifier is required.", nameof(request.BackfillId));
        }

        var booksQuery = dbContext.Books
            .AsNoTracking()
            .Where(book => !book.IsHiddenFromCatalog && book.RedirectedToIsbn13 == null);
        if (!string.IsNullOrWhiteSpace(request.Cursor))
        {
            if (!Isbn13.TryCreate(request.Cursor, out var cursor))
            {
                throw new ArgumentException("The backfill cursor must be a valid ISBN-13.", nameof(request.Cursor));
            }

            booksQuery = booksQuery.Where(book => book.Id > cursor);
        }

        var bookIds = await booksQuery
            .OrderBy(book => book.Id)
            .Take(PageSize)
            .Select(book => book.Id)
            .ToArrayAsync(cancellationToken);
        if (bookIds.Length == 0)
        {
            return new SeedRecommendationsBackfillPageResult(0, 0, null, true);
        }

        var books = bookIds.Select(bookId => bookId.Value).ToArray();
        var jobs = await dbContext.BookRecommendationJobs
            .Where(job => books.Contains(job.Isbn13))
            .ToDictionaryAsync(job => job.Isbn13, cancellationToken);
        var workItems = new List<BookRecommendationQueueItem>(books.Length);
        var now = dateTimeProvider.UtcNow;
        foreach (var isbn13 in books)
        {
            if (!jobs.TryGetValue(isbn13, out var job))
            {
                job = BookRecommendationJob.Create(isbn13);
                dbContext.BookRecommendationJobs.Add(job);
                jobs.Add(isbn13, job);
            }

            if (job.Request(now, force: job.Status == BookRecommendationJobStatus.Failed))
            {
                workItems.Add(new BookRecommendationQueueItem(isbn13, job.RequestVersion));
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        if (workItems.Count > 0)
        {
            await workQueue.EnqueueBooksAsync(workItems, cancellationToken);
        }

        var hasMore = books.Length == PageSize;
        var nextCursor = hasMore ? bookIds[^1].Value : null;
        if (hasMore)
        {
            await workQueue.EnqueueBackfillPageAsync(request.BackfillId, nextCursor, cancellationToken);
        }

        return new SeedRecommendationsBackfillPageResult(
            books.Length,
            workItems.Count,
            nextCursor,
            !hasMore);
    }
}
