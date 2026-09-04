using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.BookMovementAggregate;
using Vole_Papillon_Damour.Domain.BookMovementAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Books.Commands.Background;

public sealed class ReleaseDueAnnouncementsCommandHandler(
    IProjectDbContext dbContext,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<ReleaseDueAnnouncementsCommand, ReleaseDueAnnouncementsResult>
{
    public async Task<ReleaseDueAnnouncementsResult> Handle(
        ReleaseDueAnnouncementsCommand command,
        CancellationToken cancellationToken)
    {
        var now = dateTimeProvider.UtcNow;
        EnsureUtc(now);
        var nowOffset = new DateTimeOffset(now);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var fairs = await dbContext.AssoEvents
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        var dueFairs = fairs
            .Where(assoEvent =>
                !assoEvent.IsCancelled &&
                assoEvent.EventsType.Value == EventsType.EventsTypeEnum.Books &&
                GetOpeningInstant(assoEvent) <= nowOffset)
            .ToDictionary(assoEvent => assoEvent.Id);

        var dueAnnouncements = (await dbContext.BookAnnouncements
            .Where(announcement =>
                announcement.Status == BookAnnouncementStatus.Announced &&
                announcement.AssoEventsId != null)
            .ToListAsync(cancellationToken))
            .Where(announcement =>
                announcement.AssoEventsId is { } assoEventsId &&
                dueFairs.ContainsKey(assoEventsId))
            .OrderBy(announcement => GetOpeningInstant(dueFairs[announcement.AssoEventsId!]))
            .ThenBy(announcement => announcement.Id)
            .Select(announcement => new
            {
                Announcement = announcement,
                FairId = announcement.AssoEventsId!
            })
            .ToList();

        if (dueAnnouncements.Count == 0)
        {
            await transaction.CommitAsync(cancellationToken);
            return new ReleaseDueAnnouncementsResult(0, 0);
        }

        var isbn13s = dueAnnouncements
            .Select(candidate => candidate.Announcement.Isbn13)
            .Distinct()
            .ToArray();
        var books = await dbContext.Books
            .Where(book => isbn13s.Contains(book.Id))
            .ToDictionaryAsync(book => book.Id, cancellationToken);

        var releasedCount = 0;
        var releasedQuantity = 0;
        foreach (var candidate in dueAnnouncements)
        {
            var announcement = candidate.Announcement;
            if (!announcement.Release(now))
            {
                continue;
            }

            if (!books.TryGetValue(announcement.Isbn13, out var book))
            {
                throw new InvalidOperationException(
                    $"Announcement {announcement.Id.Value} points to missing book {announcement.Isbn13.Value}.");
            }

            for (var index = 0; index < announcement.Quantity; index++)
            {
                book.RecordAvailableEntry(now);
            }

            dbContext.BookMovements.Add(BookMovement.Create(
                BookMovementId.CreateUnique(),
                announcement.Isbn13,
                BookMovementType.FairRelease,
                announcement.Quantity,
                now,
                now,
                clockSuspect: false,
                scanSessionId: null,
                volunteerId: null,
                candidate.FairId,
                note: "RG-23",
                clientGestureId: null));

            releasedCount++;
            releasedQuantity += announcement.Quantity;
        }

        if (releasedCount > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return new ReleaseDueAnnouncementsResult(
            releasedCount,
            releasedQuantity,
            dueAnnouncements.Count - releasedCount);
    }

    private static void EnsureUtc(DateTime value)
    {
        if (value.Kind != DateTimeKind.Utc)
        {
            throw new InvalidOperationException("The worker clock must be expressed in UTC.");
        }
    }

    private static DateTimeOffset GetOpeningInstant(
        Vole_Papillon_Damour.Domain.AssoEventsAggregate.AssoEvents assoEvent)
    {
        return BookFairSchedule.GetOpeningInstant(assoEvent);
    }
}
