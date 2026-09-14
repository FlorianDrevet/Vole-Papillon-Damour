using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Books.Commands.Background;

public sealed class AttachUndatedAnnouncementsToNextFairCommandHandler(
    IProjectDbContext dbContext,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<
        AttachUndatedAnnouncementsToNextFairCommand,
        AttachUndatedAnnouncementsToNextFairResult>
{
    public async Task<AttachUndatedAnnouncementsToNextFairResult> Handle(
        AttachUndatedAnnouncementsToNextFairCommand command,
        CancellationToken cancellationToken)
    {
        var now = dateTimeProvider.UtcNow;
        if (now.Kind != DateTimeKind.Utc)
        {
            throw new InvalidOperationException("The worker clock must be expressed in UTC.");
        }

        var nowOffset = new DateTimeOffset(now);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        // Book fairs are loaded once and untracked: only their identifiers and
        // schedule are needed, not the event aggregates.
        var bookFairs = await dbContext.AssoEvents
            .AsNoTracking()
            .WhereBookFair()
            .ToListAsync(cancellationToken);
        var cancelledBookFairIds = bookFairs
            .Where(assoEvent => assoEvent.IsCancelled)
            .Select(assoEvent => assoEvent.Id)
            .ToArray();

        var detachedCount = 0;
        if (cancelledBookFairIds.Length > 0)
        {
            var announcementsOnCancelledFairs = await dbContext.BookAnnouncements
                .Where(announcement =>
                    announcement.Status == BookAnnouncementStatus.Announced &&
                    announcement.AssoEventsId != null &&
                    cancelledBookFairIds.Contains(announcement.AssoEventsId!))
                .ToListAsync(cancellationToken);
            foreach (var announcement in announcementsOnCancelledFairs)
            {
                if (announcement.DetachFromFair())
                {
                    detachedCount++;
                }
            }

            if (detachedCount > 0)
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }

        var nextFair = bookFairs
            .Where(assoEvent =>
                !assoEvent.IsCancelled &&
                GetOpeningInstant(assoEvent) > nowOffset)
            .OrderBy(GetOpeningInstant)
            .ThenBy(assoEvent => assoEvent.Id)
            .FirstOrDefault();

        if (nextFair is null)
        {
            await transaction.CommitAsync(cancellationToken);
            return new AttachUndatedAnnouncementsToNextFairResult(null, 0, detachedCount);
        }

        var announcements = await dbContext.BookAnnouncements
            .Where(announcement =>
                announcement.AssoEventsId == null &&
                announcement.Status == BookAnnouncementStatus.Announced)
            .OrderBy(announcement => announcement.CreatedAt)
            .ThenBy(announcement => announcement.Id)
            .ToListAsync(cancellationToken);

        var attachedCount = announcements.Count(announcement => announcement.AttachTo(nextFair.Id));
        if (attachedCount > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return new AttachUndatedAnnouncementsToNextFairResult(nextFair.Id, attachedCount, detachedCount);
    }

    private static DateTimeOffset GetOpeningInstant(
        Vole_Papillon_Damour.Domain.AssoEventsAggregate.AssoEvents assoEvent)
    {
        return BookFairSchedule.GetOpeningInstant(assoEvent);
    }
}
