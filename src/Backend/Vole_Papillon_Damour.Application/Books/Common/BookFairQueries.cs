using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate.Entities;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Books.Common;

/// <summary>
/// SQL-side filters for book fairs. Loading <see cref="AssoEvents"/> without them also
/// materializes every bingo event together with its owned parties, lines and lots.
/// </summary>
public static class BookFairQueries
{
    public static IQueryable<AssoEvents> WhereBookFair(this IQueryable<AssoEvents> events)
    {
        return events.Where(assoEvent =>
            assoEvent.EventsType == new EventsType(EventsType.EventsTypeEnum.Books));
    }

    public static IQueryable<AssoEvents> WhereActiveBookFair(this IQueryable<AssoEvents> events)
    {
        return events
            .WhereBookFair()
            .Where(assoEvent => !assoEvent.IsCancelled);
    }

    /// <summary>
    /// Loads only the events referenced by the given announcements.
    /// </summary>
    public static async Task<IReadOnlyList<AssoEvents>> ToReferencedFairListAsync(
        this IQueryable<AssoEvents> events,
        IEnumerable<BookAnnouncement> announcements,
        CancellationToken cancellationToken)
    {
        var fairIds = announcements
            .Where(announcement => announcement.AssoEventsId is not null)
            .Select(announcement => announcement.AssoEventsId!)
            .Distinct()
            .ToArray();

        return fairIds.Length == 0
            ? []
            : await events
                .Where(assoEvent => fairIds.Contains(assoEvent.Id))
                .ToListAsync(cancellationToken);
    }
}
