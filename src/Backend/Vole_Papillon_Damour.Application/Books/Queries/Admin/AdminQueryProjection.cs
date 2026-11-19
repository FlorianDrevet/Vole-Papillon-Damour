using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Isbndb;
using Vole_Papillon_Damour.Domain.BookAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate.Entities;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.BookMovementAggregate;

namespace Vole_Papillon_Damour.Application.Books.Queries.Admin;

internal static class AdminQueryProjection
{
    public static AdminBookResult ToBookResult(
        Book book,
        IEnumerable<BookAnnouncement> announcements,
        IEnumerable<BookMovement> movements,
        bool isRare,
        bool includeMovements = true,
        BookIsbndbRecord? isbndbRecord = null)
    {
        var movementRows = includeMovements
            ? movements
                .Where(movement => movement.Isbn13 == book.Id)
                .OrderByDescending(movement => movement.OccurredAt)
                .ThenByDescending(movement => movement.Id.Value)
                .Select(ToMovementResult)
                .ToArray()
            : [];

        return ToBookResultCore(
            AdminBookQueryProjection.FromBook(book),
            announcements,
            movementRows,
            isRare,
            isbndbRecord?.Subjects,
            book.RawPayload);
    }

    public static AdminBookResult ToBookResult(
        AdminBookQueryProjection book,
        IEnumerable<BookAnnouncement> announcements,
        bool isRare,
        string? recordSubjects,
        string? rawPayload) =>
        ToBookResultCore(book, announcements, [], isRare, recordSubjects, rawPayload);

    private static AdminBookResult ToBookResultCore(
        AdminBookQueryProjection book,
        IEnumerable<BookAnnouncement> announcements,
        IReadOnlyList<AdminBookMovementResult> movementRows,
        bool isRare,
        string? recordSubjects,
        string? rawPayload)
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
            isRare,
            book.IsHiddenFromCatalog,
            book.RedirectedToIsbn13?.Value,
            book.CoverUrl,
            new DateTimeOffset(book.FirstSeenAt, TimeSpan.Zero),
            book.LastAvailableAt is { } lastAvailableAt
                ? new DateTimeOffset(lastAvailableAt, TimeSpan.Zero)
                : null,
            new DateTimeOffset(book.UpdatedAt, TimeSpan.Zero),
            announcementRows,
            movementRows,
            book.IsIsbndbSourced,
            book.IsAbsentFromIsbndb,
            ToOffset(book.IsbndbCheckedAt),
            ToOffset(book.IsbndbCompletedAt),
            book.Pages,
            book.Edition,
            book.Summary,
            book.Genres,
            IsbndbSubjectReader.Read(recordSubjects, rawPayload),
            book.MetadataSource is { } fallback && fallback != Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects.BookMetadataSource.Isbndb
                ? fallback.ToString()
                : null);
    }

    private static DateTimeOffset? ToOffset(DateTime? value) =>
        value is { } instant ? new DateTimeOffset(DateTime.SpecifyKind(instant, DateTimeKind.Utc)) : null;

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

    private const string AnnouncementCorrectionNotePrefix = "Announcement.Correction";

    /// <summary>
    /// Movements that change the available stock. A correction only counts when it is
    /// not an announcement correction. Kept as an expression so aggregates run in SQL.
    /// </summary>
    public static readonly System.Linq.Expressions.Expression<Func<BookMovement, bool>> AffectsAvailableQuantity =
        movement =>
            movement.Type == Domain.BookMovementAggregate.ValueObjects.BookMovementType.DirectEntry ||
            movement.Type == Domain.BookMovementAggregate.ValueObjects.BookMovementType.FairRelease ||
            movement.Type == Domain.BookMovementAggregate.ValueObjects.BookMovementType.Sale ||
            movement.Type == Domain.BookMovementAggregate.ValueObjects.BookMovementType.Withdrawal ||
            (movement.Type == Domain.BookMovementAggregate.ValueObjects.BookMovementType.Correction &&
             (movement.Note == null || !movement.Note.StartsWith(AnnouncementCorrectionNotePrefix)));
}

internal sealed record AdminBookQueryProjection(
    BookCode Id,
    string? WorkId,
    string? Title,
    string? Authors,
    string? Publisher,
    int? PublicationYear,
    string? PhysicalFormat,
    string? Language,
    string? Genre,
    BookMetadataStatus MetadataStatus,
    BookMetadataSource? MetadataSource,
    string? ManuallyEditedFields,
    int QuantityAvailable,
    int SalesCount,
    int RejectionCount,
    bool IsHiddenFromCatalog,
    Isbn13? RedirectedToIsbn13,
    string? CoverUrl,
    DateTime FirstSeenAt,
    DateTime? LastAvailableAt,
    DateTime UpdatedAt,
    bool IsIsbndbSourced,
    bool IsAbsentFromIsbndb,
    DateTime? IsbndbCheckedAt,
    DateTime? IsbndbCompletedAt,
    int? Pages,
    string? Edition,
    string? Summary,
    IReadOnlyList<string> Genres)
{
    public static AdminBookQueryProjection FromBook(Book book) =>
        new(
            book.Id,
            book.WorkId,
            book.Title,
            book.Authors,
            book.Publisher,
            book.PublicationYear,
            book.PhysicalFormat,
            book.Language,
            book.Genre,
            book.MetadataStatus,
            book.MetadataSource,
            book.ManuallyEditedFields,
            book.QuantityAvailable,
            book.SalesCount,
            book.RejectionCount,
            book.IsHiddenFromCatalog,
            book.RedirectedToIsbn13,
            book.CoverUrl,
            book.FirstSeenAt,
            book.LastAvailableAt,
            book.UpdatedAt,
            book.IsIsbndbSourced,
            book.IsAbsentFromIsbndb,
            book.IsbndbCheckedAt,
            book.IsbndbCompletedAt,
            book.Pages,
            book.Edition,
            book.Summary,
            book.Genres.Select(genre => genre.Name).ToArray());
}
