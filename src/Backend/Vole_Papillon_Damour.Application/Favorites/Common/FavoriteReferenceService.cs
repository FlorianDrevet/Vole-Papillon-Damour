using System.Data;
using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.Favorites;
using Vole_Papillon_Damour.Domain.BookAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.IsbndbAggregate;
using Vole_Papillon_Damour.Domain.RecommendationAggregate;

namespace Vole_Papillon_Damour.Application.Favorites.Common;

public sealed class FavoriteReferenceService(
    IProjectDbContext dbContext,
    IBookMetadataEnrichmentQueue enrichmentQueue,
    IBookRecommendationWorkQueue recommendationWorkQueue,
    IIsbndbQuotaGuard quotaGuard,
    IDateTimeProvider dateTimeProvider,
    IOptions<FavoritesOptions> options)
{
    public async Task<ErrorOr<FavoriteReferenceResult>> EnsureAsync(
        Isbn13 isbn13,
        FavoriteOrigin origin,
        CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(origin))
        {
            return Error.Validation("Favorites.InvalidOrigin", "The favorite origin is invalid.");
        }

        var now = dateTimeProvider.UtcNow;
        if (now.Kind != DateTimeKind.Utc)
        {
            return Error.Validation("Favorites.InvalidClock", "The favorites clock must be expressed in UTC.");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var existing = await dbContext.Books.SingleOrDefaultAsync(
            book => book.Id == isbn13,
            cancellationToken);
        if (existing is not null)
        {
            var canonicalIsbn13 = existing.RedirectedToIsbn13;
            if (canonicalIsbn13 is null)
            {
                await transaction.CommitAsync(cancellationToken);
                return new FavoriteReferenceResult(existing.Id, Created: false);
            }

            var canonicalBook = await dbContext.Books.SingleOrDefaultAsync(
                book => book.Id == canonicalIsbn13.Value,
                cancellationToken);
            if (canonicalBook is null)
            {
                return Error.Unexpected(
                    "Favorites.CanonicalBookNotFound",
                    "The canonical book for this favorite reference does not exist.");
            }

            await transaction.CommitAsync(cancellationToken);
            return new FavoriteReferenceResult(canonicalBook.Id, Created: false);
        }

        var dailyUsage = await dbContext.IsbndbDailyUsages.SingleOrDefaultAsync(
            usage => usage.Day == DateOnly.FromDateTime(now) && usage.Usage == IsbndbUsage.Favorites,
            cancellationToken);
        var dailyLimitReached = options.Value.ReferenceDailyIsbndbCredits <= 0 ||
                                (dailyUsage?.Credits ?? 0) >= options.Value.ReferenceDailyIsbndbCredits;
        var forceFallback = dailyLimitReached || await quotaGuard.IsExhaustedAsync(now, cancellationToken);

        var book = Book.CreateFavoriteReference(isbn13, now);
        dbContext.Books.Add(book);

        var job = await dbContext.BookRecommendationJobs.SingleOrDefaultAsync(
            candidate => candidate.Isbn13 == isbn13.Value,
            cancellationToken);
        if (job is null)
        {
            job = BookRecommendationJob.Create(isbn13.Value);
            dbContext.BookRecommendationJobs.Add(job);
        }

        job.Request(now);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        enrichmentQueue.Enqueue(isbn13, IsbndbUsage.Favorites, forceFallback);
        await recommendationWorkQueue.EnqueueBooksAsync(
            [new BookRecommendationQueueItem(job.Isbn13, job.RequestVersion)],
            cancellationToken);

        return new FavoriteReferenceResult(isbn13, Created: true);
    }
}
