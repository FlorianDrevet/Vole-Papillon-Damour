using Vole_Papillon_Damour.Domain.AssoEventsAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate.Entities;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Books.Common;

internal static class PublicCatalogProjector
{
    public static IReadOnlyList<PublicCatalogBookResult> Project(
        IEnumerable<Book> books,
        IEnumerable<BookAnnouncement> announcements,
        IEnumerable<AssoEvents> fairs,
        DateTime nowUtc)
    {
        var fairsById = fairs
            .Where(assoEvent =>
                !assoEvent.IsCancelled &&
                assoEvent.EventsType?.Value == EventsType.EventsTypeEnum.Books)
            .ToDictionary(assoEvent => assoEvent.Id.Value);

        var availabilityByIsbn = announcements
            .Where(announcement =>
                announcement.Status == BookAnnouncementStatus.Announced &&
                (announcement.AssoEventsId is null ||
                 fairsById.ContainsKey(announcement.AssoEventsId.Value)))
            .GroupBy(announcement => announcement.Isbn13.Value, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => new AnnouncementAvailability(
                    group.Sum(announcement => announcement.Quantity),
                    group
                        .Select(announcement => GetFutureFairOpening(announcement, fairsById, nowUtc))
                        .Where(opening => opening is not null)
                        .Min()),
                StringComparer.Ordinal);

        return books
            .Where(book => !book.IsHiddenFromCatalog && book.RedirectedToIsbn13 is null)
            .Select(book =>
            {
                availabilityByIsbn.TryGetValue(book.Id.Value, out var availability);
                return new PublicCatalogBookResult(
                    book.Id.Value,
                    book.Title,
                    book.Authors,
                    book.Publisher,
                    book.PublicationYear,
                    book.PhysicalFormat,
                    book.Language,
                    book.Genre,
                    book.WorkId,
                    book.CoverBlobRef,
                    book.QuantityAvailable,
                    availability?.Quantity ?? 0,
                    availability?.NextFairAt,
                    ToOffset(book.LastAvailableAt),
                    new DateTimeOffset(book.FirstSeenAt, TimeSpan.Zero),
                    new DateTimeOffset(book.UpdatedAt, TimeSpan.Zero),
                    book.IsRare);
            })
            .ToArray();
    }

    private static DateTimeOffset? GetFutureFairOpening(
        BookAnnouncement announcement,
        IReadOnlyDictionary<Guid, AssoEvents> fairsById,
        DateTime nowUtc)
    {
        if (announcement.AssoEventsId is not { } fairId ||
            !fairsById.TryGetValue(fairId.Value, out var fair))
        {
            return null;
        }

        var opening = fair.HourOpenDoors ?? fair.DateStart;
        return opening.UtcDateTime > nowUtc ? opening : null;
    }

    private static DateTimeOffset? ToOffset(DateTime? value)
    {
        return value is { } dateTime
            ? new DateTimeOffset(dateTime, TimeSpan.Zero)
            : null;
    }

    private sealed record AnnouncementAvailability(
        int Quantity,
        DateTimeOffset? NextFairAt);
}
