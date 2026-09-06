using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.Common.Models;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.BookMovementAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;
using AssociationSettingsEntity = Vole_Papillon_Damour.Domain.AssociationSettingsAggregate.AssociationSettings;

namespace Vole_Papillon_Damour.Application.Books.Queries.Admin;

public sealed class GetCatalogAdminOverviewQueryHandler(
    IProjectDbContext dbContext,
    IDateTimeProvider dateTimeProvider,
    IBookAlertOutbox bookAlertOutbox)
    : IRequestHandler<GetCatalogAdminOverviewQuery, ErrorOr<CatalogAdminOverviewResult>>
{
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
        var from = query.From?.UtcDateTime ?? to.AddDays(-30);
        if (from.Kind != DateTimeKind.Utc || to.Kind != DateTimeKind.Utc || from >= to)
        {
            return Error.Validation("Book.InvalidPeriod", "The period must be expressed in UTC and have a positive duration.");
        }

        var duration = to - from;
        if (duration > TimeSpan.FromDays(366))
        {
            return Error.Validation("Book.InvalidPeriod", "The administration period cannot exceed one year.");
        }

        var books = await dbContext.Books.AsNoTracking().ToListAsync(cancellationToken);
        var announcements = await dbContext.BookAnnouncements
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        var movements = await dbContext.BookMovements
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        var sessions = await dbContext.ScanSessions
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        var fairs = await dbContext.AssoEvents
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        var settings = await dbContext.AssociationSettings
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id ==
                AssociationSettingsEntity.SingletonId,
                cancellationToken);

        var canonicalBooks = books
            .Where(book => book.RedirectedToIsbn13 is null)
            .ToArray();
        var bookFairIds = fairs
            .Where(fair =>
                !fair.IsCancelled &&
                fair.EventsType?.Value == EventsType.EventsTypeEnum.Books)
            .Select(fair => fair.Id.Value)
            .ToHashSet();
        var activeAnnouncements = announcements
            .Where(announcement =>
                announcement.Status == BookAnnouncementStatus.Announced &&
                (announcement.AssoEventsId is null ||
                 bookFairIds.Contains(announcement.AssoEventsId.Value)))
            .ToArray();

        var stock = new AdminStockSummaryResult(
            canonicalBooks.Sum(book => book.QuantityAvailable),
            canonicalBooks.Count(book => book.QuantityAvailable > 0),
            activeAnnouncements.Sum(announcement => announcement.Quantity),
            activeAnnouncements.Select(announcement => announcement.Isbn13.Value).Distinct().Count());

        var currentPeriod = BuildPeriodMetrics(from, to, movements, sessions);
        var previousPeriod = BuildPeriodMetrics(from - duration, from, movements, sessions);
        var lastFair = BuildLastFairSummary(fairs, movements, to);

        var deadStockCutoff = to.AddDays(-(settings?.DeadStockMinAgeDays ?? 180));
        var saleIsbns = movements
            .Where(movement => movement.Type == BookMovementType.Sale)
            .Select(movement => movement.Isbn13.Value)
            .ToHashSet(StringComparer.Ordinal);
        var firstAvailableByIsbn = movements
            .Where(AdminQueryProjection.AffectsAvailableQuantity)
            .Where(movement => movement.Quantity > 0)
            .GroupBy(movement => movement.Isbn13.Value, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Min(movement => movement.OccurredAt), StringComparer.Ordinal);
        var deadStockCount = canonicalBooks.Count(book =>
            book.QuantityAvailable > (settings?.DeadStockMinQuantity ?? 1) &&
            !saleIsbns.Contains(book.Id.Value) &&
            firstAvailableByIsbn.TryGetValue(book.Id.Value, out var firstAvailableAt) &&
            firstAvailableAt <= deadStockCutoff);

        var inventoryDriftTitleCount = 0;
        var inventoryDriftQuantity = 0;
        foreach (var book in canonicalBooks)
        {
            var ledgerQuantity = movements
                .Where(movement => movement.Isbn13 == book.Id &&
                                   AdminQueryProjection.AffectsAvailableQuantity(movement))
                .Sum(movement => movement.Quantity);
            var difference = book.QuantityAvailable - ledgerQuantity;
            if (difference != 0)
            {
                inventoryDriftTitleCount++;
                inventoryDriftQuantity += Math.Abs(difference);
            }
        }

        var pendingAlerts = await bookAlertOutbox.GetAdminPageAsync(
            BookAlertQueueStatus.Pending,
            null,
            null,
            1,
            1,
            cancellationToken);
        var pendingAlertAt = pendingAlerts.Items.FirstOrDefault()?.DueAt;

        return new CatalogAdminOverviewResult(
            new DateTimeOffset(generatedAt, TimeSpan.Zero),
            currentPeriod,
            previousPeriod,
            stock,
            lastFair,
            deadStockCount,
            canonicalBooks.Count(book => book.IsRare),
            canonicalBooks.Count(book => book.MetadataStatus is BookMetadataStatus.Pending or BookMetadataStatus.NotFound),
            announcements.Count(announcement =>
                announcement.Status == BookAnnouncementStatus.Announced &&
                announcement.AssoEventsId is null),
            inventoryDriftTitleCount,
            inventoryDriftQuantity,
            new AdminAlertQueueSummaryResult(
                pendingAlerts.TotalCount,
                pendingAlertAt is { } oldest ? new DateTimeOffset(oldest, TimeSpan.Zero) : null,
                pendingAlertAt is { } next ? new DateTimeOffset(next, TimeSpan.Zero) : null));
    }

    private static AdminPeriodMetricsResult BuildPeriodMetrics(
        DateTime from,
        DateTime to,
        IReadOnlyCollection<Domain.BookMovementAggregate.BookMovement> movements,
        IReadOnlyCollection<Domain.ScanSessionAggregate.ScanSession> sessions)
    {
        var periodSessions = sessions.Where(session =>
            session.StartedAt >= from && session.StartedAt < to);
        var sales = movements.Where(movement =>
            movement.Type == BookMovementType.Sale &&
            movement.OccurredAt >= from && movement.OccurredAt < to);
        return new AdminPeriodMetricsResult(
            new DateTimeOffset(from, TimeSpan.Zero),
            new DateTimeOffset(to, TimeSpan.Zero),
            periodSessions.Sum(session => session.ScannedCount),
            periodSessions.Sum(session => session.KeptCount),
            periodSessions.Sum(session => session.RejectedCount),
            sales.Sum(movement => Math.Abs(movement.Quantity)),
            sales.Select(movement => movement.Isbn13.Value).Distinct().Count());
    }

    private static AdminFairSummaryResult? BuildLastFairSummary(
        IReadOnlyCollection<Domain.AssoEventsAggregate.AssoEvents> fairs,
        IReadOnlyCollection<Domain.BookMovementAggregate.BookMovement> movements,
        DateTime to)
    {
        var fair = fairs
            .Where(candidate =>
                !candidate.IsCancelled &&
                candidate.EventsType?.Value == EventsType.EventsTypeEnum.Books &&
                candidate.DateStart.UtcDateTime <= to)
            .OrderByDescending(candidate => candidate.DateStart)
            .FirstOrDefault();
        if (fair is null)
        {
            return null;
        }

        var sales = movements.Where(movement =>
            movement.Type == BookMovementType.Sale &&
            movement.AssoEventsId == fair.Id);
        return new AdminFairSummaryResult(
            fair.Id.Value,
            fair.Name,
            fair.DateStart,
            fair.DateEnd,
            sales.Sum(movement => Math.Abs(movement.Quantity)),
            sales.Select(movement => movement.Isbn13.Value).Distinct().Count(),
            fair.BookRevenue);
    }
}
