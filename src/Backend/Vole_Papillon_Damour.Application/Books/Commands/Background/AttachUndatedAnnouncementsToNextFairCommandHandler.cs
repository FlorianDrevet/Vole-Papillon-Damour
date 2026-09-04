using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;

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

        var cancelledBookFairIds = (await dbContext.AssoEvents
            .ToListAsync(cancellationToken))
            .Where(assoEvent =>
                assoEvent.IsCancelled &&
                assoEvent.EventsType?.Value == EventsType.EventsTypeEnum.Books)
            .Select(assoEvent => assoEvent.Id)
            .Select(id => id.Value)
            .ToHashSet();

        var attachedAnnouncements = await dbContext.BookAnnouncements
            .Where(announcement =>
                announcement.Status == BookAnnouncementStatus.Announced &&
                announcement.AssoEventsId != null)
            .ToListAsync(cancellationToken);
        var detachedCount = 0;
        foreach (var announcement in attachedAnnouncements)
        {
            if (announcement.AssoEventsId is { } fairId &&
                cancelledBookFairIds.Contains(fairId.Value) &&
                announcement.DetachFromFair())
            {
                detachedCount++;
            }
        }

        if (detachedCount > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var nextFair = (await dbContext.AssoEvents
            .ToListAsync(cancellationToken))
            .Where(assoEvent =>
                !assoEvent.IsCancelled &&
                assoEvent.EventsType.Value == EventsType.EventsTypeEnum.Books &&
                GetOpeningInstant(assoEvent) > nowOffset)
            .OrderBy(GetOpeningInstant)
            .ThenBy(assoEvent => assoEvent.Id)
            .FirstOrDefault();

        if (nextFair is null)
        {
            if (detachedCount > 0)
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }

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
        if (attachedCount > 0 || detachedCount > 0)
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
