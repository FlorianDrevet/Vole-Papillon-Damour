using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.IsbndbAggregate;

namespace Vole_Papillon_Damour.Application.Isbndb.Queries.GetIsbndbStatus;

public sealed class GetIsbndbStatusQueryHandler(
    IProjectDbContext dbContext,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<GetIsbndbStatusQuery, IsbndbStatusResult>
{
    public async Task<IsbndbStatusResult> Handle(
        GetIsbndbStatusQuery query,
        CancellationToken cancellationToken)
    {
        var now = dateTimeProvider.UtcNow;
        var today = DateOnly.FromDateTime(now);
        var quota = await dbContext.IsbndbQuotaStates.AsNoTracking()
            .SingleOrDefaultAsync(state => state.Id == IsbndbQuotaState.SingletonId, cancellationToken)
            ?? new IsbndbQuotaState();
        var usages = await dbContext.IsbndbDailyUsages.AsNoTracking()
            .Where(item => item.Day == today)
            .OrderBy(item => item.Usage)
            .ToListAsync(cancellationToken);

        var candidates = dbContext.Books.Where(book =>
            !book.IsIsbndbSourced &&
            !book.IsAbsentFromIsbndb &&
            book.MetadataStatus != BookMetadataStatus.Discarded &&
            book.RedirectedToIsbn13 == null);
        var pendingTotal = await candidates.CountAsync(cancellationToken);
        var pendingInStock = await candidates.CountAsync(book =>
            book.QuantityAvailable > 0 || dbContext.BookAnnouncements.Any(announcement =>
                announcement.Isbn13 == book.Id && announcement.Status == BookAnnouncementStatus.Announced),
            cancellationToken);
        var absentToComplete = await dbContext.Books.AsNoTracking()
            .CountAsync(book => book.IsAbsentFromIsbndb && book.IsbndbCompletedAt == null, cancellationToken);
        var lastRun = await dbContext.IsbndbCatchUpRuns.AsNoTracking()
            .OrderByDescending(run => run.StartedAt)
            .FirstOrDefaultAsync(cancellationToken);
        var records = await dbContext.BookIsbndbRecords.AsNoTracking()
            .Select(record => new { record.Subjects, record.Publisher, record.Authors })
            .ToListAsync(cancellationToken);

        var unmatched = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var record in records)
        {
            var subjects = DeserializeStrings(record.Subjects);
            var authors = DeserializeStrings(record.Authors);
            foreach (var subject in IsbndbGenreMapper.Map(subjects, record.Publisher, authors).UnmatchedSubjects)
            {
                unmatched[subject] = unmatched.GetValueOrDefault(subject) + 1;
            }
        }

        var utcStatus = quota.Status(now).ToString();
        var inStockPending = pendingInStock;
        return new IsbndbStatusResult(
            quota.DailyLimit,
            quota.DailyRemaining,
            ToOffset(quota.ObservedAt),
            ToOffset(quota.ResetsAt),
            utcStatus,
            ToOffset(quota.ExhaustedUntil),
            usages.Select(item => new IsbndbUsageTodayResult(item.Usage.ToString(), item.Credits)).ToArray(),
            new IsbndbPendingCatchUpResult(
                pendingTotal,
                inStockPending,
                await candidates.CountAsync(book => book.MetadataSource != BookMetadataSource.Isbndb, cancellationToken)),
            absentToComplete,
            lastRun is null
                ? null
                : new IsbndbLastRunResult(
                    new DateTimeOffset(lastRun.StartedAt, TimeSpan.Zero),
                    lastRun.EndedAt is { } endedAt ? new DateTimeOffset(endedAt, TimeSpan.Zero) : null,
                    lastRun.Filled,
                    lastRun.Absent,
                    lastRun.Remaining,
                    lastRun.StopReason?.ToString()),
            unmatched.OrderByDescending(pair => pair.Value).ThenBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                .Take(10).Select(pair => new IsbndbUnmatchedSubjectResult(pair.Key, pair.Value)).ToArray());
    }

    private static IReadOnlyList<string>? DeserializeStrings(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<string[]>(json); }
        catch (JsonException) { return null; }
    }

    private static DateTimeOffset? ToOffset(DateTime? value) =>
        value is { } instant ? new DateTimeOffset(DateTime.SpecifyKind(instant, DateTimeKind.Utc)) : null;
}
