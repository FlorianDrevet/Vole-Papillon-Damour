using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Domain.AdminDashboardAggregate;
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

        var bookCounts = await dbContext.Books.AsNoTracking()
            .GroupBy(_ => 1)
            .Select(group => new
            {
                PendingTotal = group.Count(book =>
                    !book.IsIsbndbSourced &&
                    !book.IsAbsentFromIsbndb &&
                    book.MetadataStatus != BookMetadataStatus.Discarded &&
                    book.RedirectedToIsbn13 == null),
                PendingInStock = group.Count(book =>
                    !book.IsIsbndbSourced &&
                    !book.IsAbsentFromIsbndb &&
                    book.MetadataStatus != BookMetadataStatus.Discarded &&
                    book.RedirectedToIsbn13 == null &&
                    (book.QuantityAvailable > 0 || dbContext.BookAnnouncements.Any(announcement =>
                        announcement.Isbn13 == book.Id && announcement.Status == BookAnnouncementStatus.Announced))),
                PendingBeforeMigration = group.Count(book =>
                    !book.IsIsbndbSourced &&
                    !book.IsAbsentFromIsbndb &&
                    book.MetadataStatus != BookMetadataStatus.Discarded &&
                    book.RedirectedToIsbn13 == null &&
                    book.MetadataSource != BookMetadataSource.Isbndb),
                AbsentToComplete = group.Count(book =>
                    book.IsAbsentFromIsbndb &&
                    book.IsbndbCompletedAt == null &&
                    book.Title != null &&
                    book.Title.Trim() != string.Empty),
            })
            .SingleOrDefaultAsync(cancellationToken);
        var pendingTotal = bookCounts?.PendingTotal ?? 0;
        var pendingInStock = bookCounts?.PendingInStock ?? 0;
        var pendingBeforeMigration = bookCounts?.PendingBeforeMigration ?? 0;
        var absentToComplete = bookCounts?.AbsentToComplete ?? 0;
        var lastRun = await dbContext.IsbndbCatchUpRuns.AsNoTracking()
            .OrderByDescending(run => run.StartedAt)
            .FirstOrDefaultAsync(cancellationToken);
        var unmatchedSubjectsJson = await dbContext.AdminDashboardSnapshots.AsNoTracking()
            .Where(snapshot => snapshot.Id == AdminDashboardSnapshot.SingletonId)
            .Select(snapshot => snapshot.IsbndbUnmatchedSubjectsJson)
            .SingleOrDefaultAsync(cancellationToken);
        var unmatchedSubjects = DeserializeUnmatchedSubjects(unmatchedSubjectsJson);

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
                pendingBeforeMigration),
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
            unmatchedSubjects);
    }

    private static IReadOnlyList<IsbndbUnmatchedSubjectResult> DeserializeUnmatchedSubjects(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<IsbndbUnmatchedSubjectResult[]>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static DateTimeOffset? ToOffset(DateTime? value) =>
        value is { } instant ? new DateTimeOffset(DateTime.SpecifyKind(instant, DateTimeKind.Utc)) : null;
}
