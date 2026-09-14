using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.Common.Models;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.BookMovementAggregate.ValueObjects;
using AssociationSettingsEntity = Vole_Papillon_Damour.Domain.AssociationSettingsAggregate.AssociationSettings;
using AssoEventsEntity = Vole_Papillon_Damour.Domain.AssoEventsAggregate.AssoEvents;

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

        // Every figure is aggregated in SQL: the movement ledger only grows, so the
        // dashboard must never materialize it (nor the whole catalog).
        var settings = await dbContext.AssociationSettings
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id ==
                AssociationSettingsEntity.SingletonId,
                cancellationToken);
        var bookFairs = await dbContext.AssoEvents
            .AsNoTracking()
            .WhereActiveBookFair()
            .ToListAsync(cancellationToken);
        var bookFairIds = bookFairs.Select(fair => fair.Id).ToArray();

        var canonicalBooks = dbContext.Books
            .AsNoTracking()
            .Where(book => book.RedirectedToIsbn13 == null);
        var availableQuantity = await canonicalBooks.SumAsync(
            book => book.QuantityAvailable,
            cancellationToken);
        var availableTitleCount = await canonicalBooks.CountAsync(
            book => book.QuantityAvailable > 0,
            cancellationToken);
        var rareTitleCount = await canonicalBooks.CountAsync(
            book => book.IsRare,
            cancellationToken);
        var metadataToReviewCount = await canonicalBooks.CountAsync(
            book => book.MetadataStatus == BookMetadataStatus.Pending ||
                    book.MetadataStatus == BookMetadataStatus.NotFound,
            cancellationToken);

        var activeAnnouncements = dbContext.BookAnnouncements
            .AsNoTracking()
            .Where(announcement =>
                announcement.Status == BookAnnouncementStatus.Announced &&
                (announcement.AssoEventsId == null ||
                 bookFairIds.Contains(announcement.AssoEventsId!)));
        var announcedQuantity = await activeAnnouncements.SumAsync(
            announcement => announcement.Quantity,
            cancellationToken);
        var announcedTitleCount = await activeAnnouncements
            .Select(announcement => announcement.Isbn13)
            .Distinct()
            .CountAsync(cancellationToken);
        var undatedAnnouncementCount = await dbContext.BookAnnouncements
            .AsNoTracking()
            .CountAsync(
                announcement =>
                    announcement.Status == BookAnnouncementStatus.Announced &&
                    announcement.AssoEventsId == null,
                cancellationToken);

        var currentPeriod = await BuildPeriodMetricsAsync(from, to, cancellationToken);
        var previousPeriod = await BuildPeriodMetricsAsync(from - duration, from, cancellationToken);
        var lastFair = await BuildLastFairSummaryAsync(bookFairs, to, cancellationToken);

        var deadStockCutoff = to.AddDays(-(settings?.DeadStockMinAgeDays ?? 180));
        var deadStockMinQuantity = settings?.DeadStockMinQuantity ?? 1;
        var deadStockCount = await canonicalBooks
            .Where(book =>
                book.QuantityAvailable > deadStockMinQuantity &&
                !dbContext.BookMovements.Any(movement =>
                    movement.Isbn13 == book.Id &&
                    movement.Type == BookMovementType.Sale))
            .Select(book => dbContext.BookMovements
                .Where(AdminQueryProjection.AffectsAvailableQuantity)
                .Where(movement => movement.Isbn13 == book.Id && movement.Quantity > 0)
                .Min(movement => (DateTime?)movement.OccurredAt))
            .CountAsync(
                firstAvailableAt => firstAvailableAt <= deadStockCutoff,
                cancellationToken);

        // Only fiches whose stock disagrees with the ledger come back from SQL.
        var inventoryDifferences = await canonicalBooks
            .Select(book => book.QuantityAvailable - dbContext.BookMovements
                .Where(AdminQueryProjection.AffectsAvailableQuantity)
                .Where(movement => movement.Isbn13 == book.Id)
                .Sum(movement => movement.Quantity))
            .Where(difference => difference != 0)
            .ToListAsync(cancellationToken);

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
            new AdminStockSummaryResult(
                availableQuantity,
                availableTitleCount,
                announcedQuantity,
                announcedTitleCount),
            lastFair,
            deadStockCount,
            rareTitleCount,
            metadataToReviewCount,
            undatedAnnouncementCount,
            inventoryDifferences.Count,
            inventoryDifferences.Sum(difference => Math.Abs(difference)),
            new AdminAlertQueueSummaryResult(
                pendingAlerts.TotalCount,
                pendingAlertAt is { } oldest ? new DateTimeOffset(oldest, TimeSpan.Zero) : null,
                pendingAlertAt is { } next ? new DateTimeOffset(next, TimeSpan.Zero) : null));
    }

    private async Task<AdminPeriodMetricsResult> BuildPeriodMetricsAsync(
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken)
    {
        var sessions = dbContext.ScanSessions
            .AsNoTracking()
            .Where(session => session.StartedAt >= from && session.StartedAt < to);
        var sales = dbContext.BookMovements
            .AsNoTracking()
            .Where(movement =>
                movement.Type == BookMovementType.Sale &&
                movement.OccurredAt >= from &&
                movement.OccurredAt < to);

        return new AdminPeriodMetricsResult(
            new DateTimeOffset(from, TimeSpan.Zero),
            new DateTimeOffset(to, TimeSpan.Zero),
            await sessions.SumAsync(session => session.ScannedCount, cancellationToken),
            await sessions.SumAsync(session => session.KeptCount, cancellationToken),
            await sessions.SumAsync(session => session.RejectedCount, cancellationToken),
            await sales.SumAsync(movement => Math.Abs(movement.Quantity), cancellationToken),
            await sales.Select(movement => movement.Isbn13).Distinct().CountAsync(cancellationToken));
    }

    private async Task<AdminFairSummaryResult?> BuildLastFairSummaryAsync(
        IEnumerable<AssoEventsEntity> bookFairs,
        DateTime to,
        CancellationToken cancellationToken)
    {
        var fair = bookFairs
            .Where(candidate => candidate.DateStart.UtcDateTime <= to)
            .OrderByDescending(candidate => candidate.DateStart)
            .FirstOrDefault();
        if (fair is null)
        {
            return null;
        }

        var fairId = fair.Id;
        var sales = dbContext.BookMovements
            .AsNoTracking()
            .Where(movement =>
                movement.Type == BookMovementType.Sale &&
                movement.AssoEventsId == fairId);
        return new AdminFairSummaryResult(
            fair.Id.Value,
            fair.Name,
            fair.DateStart,
            fair.DateEnd,
            await sales.SumAsync(movement => Math.Abs(movement.Quantity), cancellationToken),
            await sales.Select(movement => movement.Isbn13).Distinct().CountAsync(cancellationToken),
            fair.BookRevenue);
    }
}
