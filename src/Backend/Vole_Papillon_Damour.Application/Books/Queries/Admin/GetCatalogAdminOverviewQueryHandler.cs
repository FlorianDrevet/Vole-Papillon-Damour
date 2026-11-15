using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Domain.AdminDashboardAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.BookMovementAggregate;
using Vole_Papillon_Damour.Domain.BookMovementAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate;
using AssoEventsEntity = Vole_Papillon_Damour.Domain.AssoEventsAggregate.AssoEvents;

namespace Vole_Papillon_Damour.Application.Books.Queries.Admin;

public sealed class GetCatalogAdminOverviewQueryHandler(
    IProjectDbContext dbContext,
    IDateTimeProvider dateTimeProvider,
    HybridCache cache)
    : IRequestHandler<GetCatalogAdminOverviewQuery, ErrorOr<CatalogAdminOverviewResult>>
{
    private static readonly HybridCacheEntryOptions CacheOptions = new()
    {
        Expiration = TimeSpan.FromSeconds(30),
        LocalCacheExpiration = TimeSpan.FromSeconds(30),
    };

    public async Task<ErrorOr<CatalogAdminOverviewResult>> Handle(
        GetCatalogAdminOverviewQuery query,
        CancellationToken cancellationToken)
    {
        var generatedAt = dateTimeProvider.UtcNow;
        if (generatedAt.Kind != DateTimeKind.Utc)
        {
            return Error.Validation("Book.InvalidClock", "The administration clock must be expressed in UTC.");
        }

        var to = query.To?.UtcDateTime ?? generatedAt;
        var from = query.AllTime
            ? DateTimeOffset.MinValue.UtcDateTime
            : query.From?.UtcDateTime ?? to.AddDays(-30);
        if (from.Kind != DateTimeKind.Utc || to.Kind != DateTimeKind.Utc || from >= to)
        {
            return Error.Validation("Book.InvalidPeriod", "The period must be expressed in UTC and have a positive duration.");
        }

        var duration = to - from;
        if (!query.AllTime && duration > TimeSpan.FromDays(366))
        {
            return Error.Validation("Book.InvalidPeriod", "The administration period cannot exceed one year.");
        }

        var cacheKey = $"admin:catalog-overview:{FormatCachePeriod(query.From)}:{FormatCachePeriod(query.To)}:{query.AllTime}";
        return await cache.GetOrCreateAsync(
            cacheKey,
            new OverviewState(this, from, to, generatedAt, duration, query.AllTime),
            static (state, token) => state.Handler.BuildOverviewAsync(
                state.From,
                state.To,
                state.GeneratedAt,
                state.Duration,
                state.AllTime,
                token),
            CacheOptions,
            cancellationToken: cancellationToken);
    }

    private async ValueTask<CatalogAdminOverviewResult> BuildOverviewAsync(
        DateTime from,
        DateTime to,
        DateTime generatedAt,
        TimeSpan duration,
        bool allTime,
        CancellationToken cancellationToken)
    {
        // Every figure is aggregated in SQL: the movement ledger only grows, so the
        // dashboard must never materialize it (nor the whole catalog).
        var dashboardSnapshot = await dbContext.AdminDashboardSnapshots
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == AdminDashboardSnapshot.SingletonId, cancellationToken);
        var bookFairs = await dbContext.AssoEvents
            .AsNoTracking()
            .WhereActiveBookFair()
            .ToListAsync(cancellationToken);
        var bookFairIds = bookFairs.Select(fair => fair.Id).ToArray();

        var bookSummary = await dbContext.Books
            .AsNoTracking()
            .GroupBy(_ => 1)
            .Select(books => new
            {
                AvailableQuantity = books.Sum(book => book.RedirectedToIsbn13 == null ? book.QuantityAvailable : 0),
                AvailableTitleCount = books.Count(book =>
                    book.RedirectedToIsbn13 == null && book.QuantityAvailable > 0),
                IsbndbAbsentToCompleteCount = books.Count(book =>
                    book.IsAbsentFromIsbndb && book.IsbndbCompletedAt == null),
            })
            .SingleOrDefaultAsync(cancellationToken);

        var activeAnnouncements = dbContext.BookAnnouncements
            .AsNoTracking()
            .Where(announcement =>
                announcement.Status == BookAnnouncementStatus.Announced &&
                (announcement.AssoEventsId == null ||
                 bookFairIds.Contains(announcement.AssoEventsId!)));
        var announcementSummary = await activeAnnouncements
            .GroupBy(_ => 1)
            .Select(announcements => new
            {
                AnnouncedQuantity = announcements.Sum(announcement => announcement.Quantity),
                AnnouncedTitleCount = announcements.Select(announcement => announcement.Isbn13).Distinct().Count(),
                UndatedAnnouncementCount = announcements.Count(announcement => announcement.AssoEventsId == null),
            })
            .SingleOrDefaultAsync(cancellationToken);

        DateTime? previousFrom = allTime ? null : from - duration;
        var lastFair = FindLastFair(bookFairs, to);
        var sessionMetrics = await BuildSessionMetricsAsync(from, to, previousFrom, cancellationToken);
        var salesMetrics = await BuildSalesMetricsAsync(from, to, previousFrom, lastFair, cancellationToken);
        var currentPeriod = ToPeriodResult(
            from,
            to,
            sessionMetrics?.CurrentScannedCount ?? 0,
            sessionMetrics?.CurrentKeptCount ?? 0,
            sessionMetrics?.CurrentRejectedCount ?? 0,
            salesMetrics?.CurrentSoldQuantity ?? 0,
            salesMetrics?.CurrentSoldTitleCount ?? 0);
        var previousPeriod = allTime
            ? EmptyPeriod(from, from)
            : ToPeriodResult(
                previousFrom!.Value,
                from,
                sessionMetrics?.PreviousScannedCount ?? 0,
                sessionMetrics?.PreviousKeptCount ?? 0,
                sessionMetrics?.PreviousRejectedCount ?? 0,
                salesMetrics?.PreviousSoldQuantity ?? 0,
                salesMetrics?.PreviousSoldTitleCount ?? 0);

        return new CatalogAdminOverviewResult(
            new DateTimeOffset(generatedAt, TimeSpan.Zero),
            currentPeriod,
            previousPeriod,
            new AdminStockSummaryResult(
                bookSummary?.AvailableQuantity ?? 0,
                bookSummary?.AvailableTitleCount ?? 0,
                announcementSummary?.AnnouncedQuantity ?? 0,
                announcementSummary?.AnnouncedTitleCount ?? 0),
            ToLastFairSummary(lastFair, salesMetrics),
            dashboardSnapshot?.DeadStockCount,
            dashboardSnapshot is null
                ? null
                : new DateTimeOffset(dashboardSnapshot.ComputedAt, TimeSpan.Zero),
            announcementSummary?.UndatedAnnouncementCount ?? 0,
            bookSummary?.IsbndbAbsentToCompleteCount ?? 0);
    }

    private async Task<SessionMetricAggregate?> BuildSessionMetricsAsync(
        DateTime from,
        DateTime to,
        DateTime? previousFrom,
        CancellationToken cancellationToken)
    {
        var lowerBound = previousFrom ?? from;
        return await dbContext.ScanSessions
            .AsNoTracking()
            .Where(session => session.StartedAt >= lowerBound && session.StartedAt < to)
            .GroupBy(_ => 1)
            .Select(sessions => new SessionMetricAggregate(
                sessions.Where(session => session.StartedAt >= from).Sum(session => session.ScannedCount),
                sessions.Where(session => session.StartedAt >= from).Sum(session => session.KeptCount),
                sessions.Where(session => session.StartedAt >= from).Sum(session => session.RejectedCount),
                sessions.Where(session => session.StartedAt < from).Sum(session => session.ScannedCount),
                sessions.Where(session => session.StartedAt < from).Sum(session => session.KeptCount),
                sessions.Where(session => session.StartedAt < from).Sum(session => session.RejectedCount)))
            .SingleOrDefaultAsync(cancellationToken);
    }

    private async Task<SalesMetricAggregate?> BuildSalesMetricsAsync(
        DateTime from,
        DateTime to,
        DateTime? previousFrom,
        AssoEventsEntity? lastFair,
        CancellationToken cancellationToken)
    {
        var lowerBound = previousFrom ?? from;
        var lastFairId = lastFair?.Id;
        return await dbContext.BookMovements
            .AsNoTracking()
            .Where(movement =>
                movement.Type == BookMovementType.Sale &&
                ((movement.OccurredAt >= lowerBound && movement.OccurredAt < to) ||
                 (lastFairId != null && movement.AssoEventsId == lastFairId)))
            .GroupBy(_ => 1)
            .Select(sales => new SalesMetricAggregate(
                sales.Where(movement => movement.OccurredAt >= from && movement.OccurredAt < to)
                    .Sum(movement => Math.Abs(movement.Quantity)),
                sales.Where(movement => movement.OccurredAt >= from && movement.OccurredAt < to)
                    .Select(movement => movement.Isbn13).Distinct().Count(),
                sales.Where(movement => movement.OccurredAt >= lowerBound && movement.OccurredAt < from)
                    .Sum(movement => Math.Abs(movement.Quantity)),
                sales.Where(movement => movement.OccurredAt >= lowerBound && movement.OccurredAt < from)
                    .Select(movement => movement.Isbn13).Distinct().Count(),
                lastFairId == null
                    ? 0
                    : sales.Where(movement => movement.AssoEventsId == lastFairId)
                        .Sum(movement => Math.Abs(movement.Quantity)),
                lastFairId == null
                    ? 0
                    : sales.Where(movement => movement.AssoEventsId == lastFairId)
                        .Select(movement => movement.Isbn13).Distinct().Count()))
            .SingleOrDefaultAsync(cancellationToken);
    }

    private static AssoEventsEntity? FindLastFair(IEnumerable<AssoEventsEntity> bookFairs, DateTime to) =>
        bookFairs
            .Where(candidate => candidate.DateStart.UtcDateTime <= to)
            .OrderByDescending(candidate => candidate.DateStart)
            .FirstOrDefault();

    private static AdminFairSummaryResult? ToLastFairSummary(
        AssoEventsEntity? fair,
        SalesMetricAggregate? sales)
    {
        if (fair is null)
        {
            return null;
        }

        return new AdminFairSummaryResult(
            fair.Id.Value,
            fair.Name,
            fair.DateStart,
            fair.DateEnd,
            sales?.LastFairSoldQuantity ?? 0,
            sales?.LastFairSoldTitleCount ?? 0,
            fair.BookRevenue);
    }

    private static AdminPeriodMetricsResult ToPeriodResult(
        DateTime from,
        DateTime to,
        int scannedCount,
        int keptCount,
        int rejectedCount,
        int soldQuantity,
        int soldTitleCount) =>
        new(
            new DateTimeOffset(from, TimeSpan.Zero),
            new DateTimeOffset(to, TimeSpan.Zero),
            scannedCount,
            keptCount,
            rejectedCount,
            soldQuantity,
            soldTitleCount);

    private static AdminPeriodMetricsResult EmptyPeriod(DateTime from, DateTime to) =>
        new(new DateTimeOffset(from, TimeSpan.Zero), new DateTimeOffset(to, TimeSpan.Zero), 0, 0, 0, 0, 0);

    private static string FormatCachePeriod(DateTimeOffset? value) =>
        value?.UtcDateTime.Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "default";

    private sealed record OverviewState(
        GetCatalogAdminOverviewQueryHandler Handler,
        DateTime From,
        DateTime To,
        DateTime GeneratedAt,
        TimeSpan Duration,
        bool AllTime);

    private sealed record SessionMetricAggregate(
        int CurrentScannedCount,
        int CurrentKeptCount,
        int CurrentRejectedCount,
        int PreviousScannedCount,
        int PreviousKeptCount,
        int PreviousRejectedCount);

    private sealed record SalesMetricAggregate(
        int CurrentSoldQuantity,
        int CurrentSoldTitleCount,
        int PreviousSoldQuantity,
        int PreviousSoldTitleCount,
        int LastFairSoldQuantity,
        int LastFairSoldTitleCount);
}
