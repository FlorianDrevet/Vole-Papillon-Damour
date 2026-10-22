using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
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
            booksQuery = booksQuery.Where(book => book.Id.Value.CompareTo(request.Cursor) > 0);
        }

        var books = await booksQuery
            .OrderBy(book => book.Id.Value)
            .Take(PageSize)
            .Select(book => book.Id.Value)
            .ToArrayAsync(cancellationToken);
        if (books.Length == 0)
        {
            return new SeedRecommendationsBackfillPageResult(0, 0, null, true);
        }

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

            if (job.Request(now))
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
        var nextCursor = hasMore ? books[^1] : null;
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
