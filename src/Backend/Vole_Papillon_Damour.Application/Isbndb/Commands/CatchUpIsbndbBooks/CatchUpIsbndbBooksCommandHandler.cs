using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Domain.BookAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate.Entities;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.IsbndbAggregate;
using Vole_Papillon_Damour.Domain.ManualRequestAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.RecommendationAggregate;
using Vole_Papillon_Damour.Domain.WatchlistAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Isbndb.Commands.CatchUpIsbndbBooks;

public sealed class CatchUpIsbndbBooksCommandHandler(
    IProjectDbContext dbContext,
    IIsbndbLookupService lookupService,
    IBibliographicMetadataResolver metadataResolver,
    IDateTimeProvider dateTimeProvider,
    IBookRecommendationWorkQueue? recommendationWorkQueue = null)
    : IRequestHandler<CatchUpIsbndbBooksCommand, CatchUpIsbndbBooksResult>
{
    private const int MaximumBulkSize = 100;
    private const int WorkKeyBatchSize = 500;

    public async Task<CatchUpIsbndbBooksResult> Handle(
        CatchUpIsbndbBooksCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.BulkSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(command.BulkSize));
        }

        if (command.AbsentRecheckDays < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(command.AbsentRecheckDays));
        }

        var bulkSize = Math.Min(command.BulkSize, MaximumBulkSize);
        var startedAt = GetUtcNow();
        IsbndbCatchUpRun? run = null;
        var filled = 0;
        var absent = 0;
        var rechecked = 0;
        var stopReason = IsbndbCatchUpStopReason.Completed;

        try
        {
            await BackfillWorkKeysAsync(cancellationToken);

            run = IsbndbCatchUpRun.Create(startedAt);
            dbContext.IsbndbCatchUpRuns.Add(run);
            await dbContext.SaveChangesAsync(cancellationToken);

            var catchUpStopReason = await ProcessCandidatesAsync(
                bulkSize,
                now => filled += now.Filled,
                now => absent += now.Absent,
                cancellationToken);
            if (catchUpStopReason is not null)
            {
                stopReason = catchUpStopReason.Value;
            }

            if (stopReason == IsbndbCatchUpStopReason.Completed)
            {
                var recheckStopReason = await RecheckAbsentBooksAsync(
                    bulkSize,
                    command.AbsentRecheckDays,
                    now => filled += now.Filled,
                    now => rechecked += now.Rechecked,
                    cancellationToken);
                if (recheckStopReason is not null)
                {
                    stopReason = recheckStopReason.Value;
                }
            }

            return await FinishRunAsync(
                run,
                startedAt,
                filled,
                absent,
                rechecked,
                stopReason,
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await FinishRunAsync(
                run,
                startedAt,
                filled,
                absent,
                rechecked,
                IsbndbCatchUpStopReason.Cancelled,
                CancellationToken.None);
            throw;
        }
        catch
        {
            await FinishRunAsync(
                run,
                startedAt,
                filled,
                absent,
                rechecked,
                IsbndbCatchUpStopReason.Unavailable,
                CancellationToken.None);
            throw;
        }
    }

    private async Task BackfillWorkKeysAsync(CancellationToken cancellationToken)
    {
        var isbn13s = await dbContext.Books
            .AsNoTracking()
            .Where(book => book.WorkKey == null && book.Title != null && book.Authors != null)
            .OrderBy(book => book.Id)
            .Select(book => book.Id)
            .ToListAsync(cancellationToken);

        foreach (var batch in isbn13s.Chunk(WorkKeyBatchSize))
        {
            var batchIds = batch.ToArray();
            var books = await dbContext.Books
                .Where(book => batchIds.Contains(book.Id))
                .ToListAsync(cancellationToken);
            foreach (var book in books)
            {
                book.ComputeWorkKeyIfMissing();
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task<IsbndbCatchUpStopReason?> ProcessCandidatesAsync(
        int bulkSize,
        Action<PageCounts> addFilled,
        Action<PageCounts> addAbsent,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var candidateIds = await GetCandidateIdsAsync(bulkSize, cancellationToken);
            if (candidateIds.Count == 0)
            {
                return null;
            }

            var lookups = await lookupService.LookupManyAsync(
                candidateIds,
                IsbndbUsage.CatchUp,
                cancellationToken);
            var books = await LoadBooksAsync(candidateIds, cancellationToken);
            var existingRecordIds = await dbContext.BookIsbndbRecords
                .AsNoTracking()
                .Where(record => candidateIds.Contains(record.Isbn13))
                .Select(record => record.Isbn13)
                .ToHashSetAsync(cancellationToken);
            var queueItems = new List<BookRecommendationQueueItem>();
            var pageCounts = new PageCounts();
            var stopReason = (IsbndbCatchUpStopReason?)null;

            foreach (var isbn13 in candidateIds)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!lookups.TryGetValue(isbn13, out var lookup))
                {
                    stopReason ??= IsbndbCatchUpStopReason.Unavailable;
                    continue;
                }

                if (lookup is IsbndbLookup.QuotaExhausted)
                {
                    stopReason = IsbndbCatchUpStopReason.QuotaExhausted;
                    continue;
                }

                if (lookup is IsbndbLookup.Unavailable)
                {
                    stopReason ??= IsbndbCatchUpStopReason.Unavailable;
                    continue;
                }

                if (!books.TryGetValue(isbn13, out var book))
                {
                    continue;
                }

                if (lookup is IsbndbLookup.NotFound)
                {
                    book.MarkAbsentFromIsbndb(GetUtcNow());
                    pageCounts.Absent++;
                    await RequestRecommendationAsync(book, queueItems, cancellationToken);
                    continue;
                }

                if (lookup is IsbndbLookup.Found found)
                {
                    try
                    {
                        await ApplyIsbndbNoticeAsync(
                            book,
                            found.Book,
                            existingRecordIds.Contains(isbn13),
                            queueItems,
                            cancellationToken);
                        pageCounts.Filled++;
                    }
                    catch (ArgumentException)
                    {
                        stopReason ??= IsbndbCatchUpStopReason.Unavailable;
                    }
                }
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await EnqueueRecommendationsAsync(queueItems, cancellationToken);
            addFilled(pageCounts);
            addAbsent(pageCounts);

            if (stopReason is not null)
            {
                return stopReason;
            }
        }
    }

    private async Task<IsbndbCatchUpStopReason?> RecheckAbsentBooksAsync(
        int bulkSize,
        int absentRecheckDays,
        Action<PageCounts> addFilled,
        Action<PageCounts> addRechecked,
        CancellationToken cancellationToken)
    {
        var cutoff = GetUtcNow().AddDays(-absentRecheckDays);
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var staleIds = await GetStaleAbsentIdsAsync(bulkSize, cutoff, cancellationToken);
            if (staleIds.Count == 0)
            {
                return null;
            }

            var books = await LoadBooksAsync(staleIds, cancellationToken);
            var existingRecordIds = await dbContext.BookIsbndbRecords
                .AsNoTracking()
                .Where(record => staleIds.Contains(record.Isbn13))
                .Select(record => record.Isbn13)
                .ToHashSetAsync(cancellationToken);
            var queueItems = new List<BookRecommendationQueueItem>();
            var pageCounts = new PageCounts();
            var stopReason = (IsbndbCatchUpStopReason?)null;

            foreach (var isbn13 in staleIds)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var lookup = await lookupService.LookupAsync(isbn13, IsbndbUsage.CatchUp, cancellationToken);
                if (lookup is IsbndbLookup.QuotaExhausted)
                {
                    stopReason = IsbndbCatchUpStopReason.QuotaExhausted;
                    break;
                }

                if (lookup is IsbndbLookup.Unavailable)
                {
                    stopReason = IsbndbCatchUpStopReason.Unavailable;
                    break;
                }

                if (!books.TryGetValue(isbn13, out var book))
                {
                    continue;
                }

                if (lookup is IsbndbLookup.NotFound)
                {
                    book.RecordIsbndbCheck(GetUtcNow());
                    pageCounts.Rechecked++;
                    continue;
                }

                if (lookup is IsbndbLookup.Found found)
                {
                    try
                    {
                        await ApplyIsbndbNoticeAsync(
                            book,
                            found.Book,
                            existingRecordIds.Contains(isbn13),
                            queueItems,
                            cancellationToken);
                        pageCounts.Filled++;
                        pageCounts.Rechecked++;
                    }
                    catch (ArgumentException)
                    {
                        stopReason = IsbndbCatchUpStopReason.Unavailable;
                        break;
                    }
                }
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await EnqueueRecommendationsAsync(queueItems, cancellationToken);
            addFilled(pageCounts);
            addRechecked(pageCounts);

            if (stopReason is not null)
            {
                return stopReason;
            }
        }
    }

    private async Task ApplyIsbndbNoticeAsync(
        Book book,
        IsbndbBook notice,
        bool recordExists,
        ICollection<BookRecommendationQueueItem> queueItems,
        CancellationToken cancellationToken)
    {
        var patch = IsbndbNoticeMapper.ToPatch(notice);
        if (patch.Fields.Count == 0)
        {
            throw new ArgumentException("The ISBNdb notice contains no usable metadata.", nameof(notice));
        }

        if (book.WorkId is null && !book.IsMetadataFieldManuallyEdited(BookMetadataField.WorkId))
        {
            var workId = await ResolveWorkIdSafelyAsync(book.Id, cancellationToken);
            if (!string.IsNullOrWhiteSpace(workId))
            {
                patch = patch with
                {
                    WorkId = workId.Trim(),
                    Fields = patch.Fields.Append(BookMetadataField.WorkId).ToArray()
                };
            }
        }

        book.ApplyIsbndbNotice(patch, GetUtcNow());
        var record = IsbndbNoticeMapper.ToRecord(notice, GetUtcNow());
        if (recordExists)
        {
            dbContext.BookIsbndbRecords.Update(record);
        }
        else
        {
            dbContext.BookIsbndbRecords.Add(record);
        }

        await RequestRecommendationAsync(book, queueItems, cancellationToken);
    }

    private async Task<string?> ResolveWorkIdSafelyAsync(Isbn13 isbn13, CancellationToken cancellationToken)
    {
        try
        {
            return await metadataResolver.ResolveWorkIdAsync(isbn13, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }

    private async Task RequestRecommendationAsync(
        Book book,
        ICollection<BookRecommendationQueueItem> queueItems,
        CancellationToken cancellationToken)
    {
        if (recommendationWorkQueue is null)
        {
            return;
        }

        var job = await dbContext.BookRecommendationJobs
            .SingleOrDefaultAsync(candidate => candidate.Isbn13 == book.Id.Value, cancellationToken);
        if (job is not null && job.Request(GetUtcNow(), force: true))
        {
            queueItems.Add(new BookRecommendationQueueItem(job.Isbn13, job.RequestVersion));
        }
    }

    private async Task EnqueueRecommendationsAsync(
        IReadOnlyCollection<BookRecommendationQueueItem> queueItems,
        CancellationToken cancellationToken)
    {
        if (recommendationWorkQueue is not null && queueItems.Count > 0)
        {
            await recommendationWorkQueue.EnqueueBooksAsync(queueItems, cancellationToken);
        }
    }

    private async Task<List<Isbn13>> GetCandidateIdsAsync(int pageSize, CancellationToken cancellationToken)
    {
        var query = CandidateQuery();
        return await OrderByPriority(query)
            .ThenBy(book => book.Id)
            .Take(pageSize)
            .Select(book => book.Id)
            .ToListAsync(cancellationToken);
    }

    private async Task<List<Isbn13>> GetStaleAbsentIdsAsync(
        int pageSize,
        DateTime cutoff,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Books
            .Where(book => book.IsAbsentFromIsbndb &&
                           book.IsbndbCompletedAt == null &&
                           book.IsbndbCheckedAt != null &&
                           book.IsbndbCheckedAt < cutoff);
        return await OrderByPriority(query)
            .ThenBy(book => book.Id)
            .Take(pageSize)
            .Select(book => book.Id)
            .ToListAsync(cancellationToken);
    }

    private IQueryable<Book> CandidateQuery() => dbContext.Books
        .Where(book =>
            !book.IsIsbndbSourced &&
            !book.IsAbsentFromIsbndb &&
            book.MetadataStatus != BookMetadataStatus.Discarded &&
            book.RedirectedToIsbn13 == null);

    private IOrderedQueryable<Book> OrderByPriority(IQueryable<Book> query) => query
        .OrderByDescending(book => book.QuantityAvailable > 0 ||
                                   dbContext.BookAnnouncements.Any(announcement =>
                                       announcement.Isbn13 == book.Id &&
                                       announcement.Status == BookAnnouncementStatus.Announced))
        .ThenByDescending(book =>
            dbContext.WatchlistItems.Any(item =>
                item.Isbn13 == book.Id &&
                dbContext.Watchlists.Any(watchlist =>
                    watchlist.Id == item.UserId &&
                    watchlist.AlertStatus == WatchlistAlertStatus.Active)) ||
            (book.WorkId != null &&
             dbContext.WatchlistItems.Any(item =>
                 item.WorkId == book.WorkId &&
                 dbContext.Watchlists.Any(watchlist =>
                     watchlist.Id == item.UserId &&
                     watchlist.AlertStatus == WatchlistAlertStatus.Active))) ||
            dbContext.ManualBookRequests.Any(request =>
                request.Isbn13 == book.Id &&
                (request.Status == ManualBookRequestStatus.Searching ||
                 request.Status == ManualBookRequestStatus.Found)) ||
            (book.WorkId != null &&
             dbContext.ManualBookRequests.Any(request =>
                 request.WorkId == book.WorkId &&
                 (request.Status == ManualBookRequestStatus.Searching ||
                  request.Status == ManualBookRequestStatus.Found))))
        .ThenByDescending(book => book.FirstSeenAt);

    private async Task<Dictionary<Isbn13, Book>> LoadBooksAsync(
        IReadOnlyList<Isbn13> isbn13s,
        CancellationToken cancellationToken) => await dbContext.Books
            .Include(book => book.Genres)
            .Where(book => isbn13s.Contains(book.Id))
            .ToDictionaryAsync(book => book.Id, cancellationToken);

    private async Task<CatchUpIsbndbBooksResult> FinishRunAsync(
        IsbndbCatchUpRun? run,
        DateTime startedAt,
        int filled,
        int absent,
        int rechecked,
        IsbndbCatchUpStopReason stopReason,
        CancellationToken cancellationToken)
    {
        if (run is null)
        {
            run = IsbndbCatchUpRun.Create(startedAt);
            dbContext.IsbndbCatchUpRuns.Add(run);
        }

        var remaining = await CandidateQuery().CountAsync(cancellationToken);
        run.Finish(GetUtcNow(), filled, absent, rechecked, remaining, stopReason);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new CatchUpIsbndbBooksResult(run.Id, filled, absent, rechecked, remaining, stopReason);
    }

    private DateTime GetUtcNow()
    {
        var now = dateTimeProvider.UtcNow;
        if (now.Kind != DateTimeKind.Utc)
        {
            throw new InvalidOperationException("The ISBNdb catch-up clock must be expressed in UTC.");
        }

        return now;
    }

    private sealed class PageCounts
    {
        public int Filled { get; set; }
        public int Absent { get; set; }
        public int Rechecked { get; set; }
    }
}
