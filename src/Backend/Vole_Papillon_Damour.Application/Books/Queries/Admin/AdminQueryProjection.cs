using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Domain.BookAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate.Entities;
using Vole_Papillon_Damour.Domain.BookMovementAggregate;

namespace Vole_Papillon_Damour.Application.Books.Queries.Admin;

internal static class AdminQueryProjection
{
    public static AdminBookResult ToBookResult(
        Book book,
        IEnumerable<BookAnnouncement> announcements,
        IEnumerable<BookMovement> movements,
        bool includeMovements = true)
    {
        var announcementRows = announcements
            .Where(announcement => announcement.Isbn13 == book.Id)
            .OrderByDescending(announcement => announcement.CreatedAt)
            .ThenBy(announcement => announcement.Id.Value)
            .Select(announcement => new AdminAnnouncementResult(
                announcement.Id.Value,
                announcement.Isbn13.Value,
                announcement.AssoEventsId?.Value,
                announcement.Quantity,
                announcement.Status.ToString(),
                new DateTimeOffset(announcement.CreatedAt, TimeSpan.Zero),
                announcement.ReleasedAt is { } releasedAt
                    ? new DateTimeOffset(releasedAt, TimeSpan.Zero)
                    : null,
                announcement.ScanSessionId.Value))
            .ToArray();

        var movementRows = includeMovements
            ? movements
                .Where(movement => movement.Isbn13 == book.Id)
                .OrderByDescending(movement => movement.OccurredAt)
                .ThenByDescending(movement => movement.Id.Value)
                .Select(ToMovementResult)
                .ToArray()
            : [];

        return new AdminBookResult(
            book.Id.Value,
            book.WorkId,
            book.Title,
            book.Authors,
            book.Publisher,
            book.PublicationYear,
            book.PhysicalFormat,
            book.Language,
            book.Genre,
            book.MetadataStatus.ToString(),
            book.MetadataSource?.ToString(),
            book.ManuallyEditedFields,
            book.QuantityAvailable,
            announcementRows
                .Where(announcement => announcement.Status == "Announced")
                .Sum(announcement => announcement.Quantity),
            book.SalesCount,
            book.RejectionCount,
            book.IsRare,
            book.IsHiddenFromCatalog,
            book.RedirectedToIsbn13?.Value,
            book.CoverBlobRef,
            new DateTimeOffset(book.FirstSeenAt, TimeSpan.Zero),
            book.LastAvailableAt is { } lastAvailableAt
                ? new DateTimeOffset(lastAvailableAt, TimeSpan.Zero)
                : null,
            new DateTimeOffset(book.UpdatedAt, TimeSpan.Zero),
            announcementRows,
            movementRows);
    }

    public static AdminBookMovementResult ToMovementResult(BookMovement movement)
    {
        return new AdminBookMovementResult(
            movement.Id.Value,
            movement.Isbn13.Value,
            movement.Type.ToString(),
            movement.Quantity,
            new DateTimeOffset(movement.OccurredAt, TimeSpan.Zero),
            new DateTimeOffset(movement.ReceivedAt, TimeSpan.Zero),
            movement.ClockSuspect,
            movement.ScanSessionId?.Value,
            movement.VolunteerId?.Value,
            movement.AssoEventsId?.Value,
            movement.Note,
            movement.ClientGestureId,
            movement.ReversalOfMovementId?.Value);
    }

    public static bool AffectsAvailableQuantity(BookMovement movement)
    {
        return movement.Type is
            Domain.BookMovementAggregate.ValueObjects.BookMovementType.DirectEntry or
            Domain.BookMovementAggregate.ValueObjects.BookMovementType.FairRelease or
            Domain.BookMovementAggregate.ValueObjects.BookMovementType.Sale or
            Domain.BookMovementAggregate.ValueObjects.BookMovementType.Withdrawal ||
            (movement.Type == Domain.BookMovementAggregate.ValueObjects.BookMovementType.Correction &&
             movement.Note?.StartsWith("Announcement.Correction", StringComparison.Ordinal) != true);
    }
}
