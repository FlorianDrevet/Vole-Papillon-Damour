using System.Linq.Expressions;
using Vole_Papillon_Damour.Domain.BookAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.BookMovementAggregate;
using Vole_Papillon_Damour.Domain.BookMovementAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Books.Common;

internal sealed record StatisticsBookProjection(
    BookCode Isbn13,
    string? Title,
    string? Authors,
    string? Genre);

internal sealed record StatisticsBookMovementProjection(
    BookMovementId Id,
    BookCode Isbn13,
    BookMovementType Type,
    int Quantity,
    DateTime OccurredAt,
    UserId? VolunteerId,
    AssoEventsId? AssoEventsId,
    ScanSessionId? ScanSessionId,
    BookMovementId? ReversalOfMovementId);

internal sealed record StatisticsCatalogueMovementProjection(
    BookMovementId Id,
    BookCode Isbn13,
    BookMovementType Type,
    int Quantity,
    DateTime OccurredAt,
    BookMovementId? ReversalOfMovementId);

internal sealed record StatisticsScanSessionProjection(
    ScanSessionId Id,
    UserId VolunteerId,
    ScanMode Mode,
    DateTime StartedAt,
    DateTime LastScanAt,
    DateTime? EndedAt,
    int ScannedCount,
    int KeptCount,
    int RejectedCount);

internal sealed record StatisticsAdminScanSessionProjection(
    ScanSessionId Id,
    UserId VolunteerId,
    DateTime StartedAt,
    DateTime LastScanAt,
    DateTime? EndedAt,
    int ScannedCount,
    int KeptCount,
    int RejectedCount);

internal sealed record StatisticsSessionKeptRateProjection(int KeptCount, int ScannedCount);

internal sealed record StatisticsVolunteerNameProjection(
    UserId Id,
    string? FirstName,
    string? LastName);

internal static class StatisticsProjectionExpressions
{
    public static readonly Expression<Func<BookMovement, StatisticsBookMovementProjection>> BookMovement = movement =>
        new StatisticsBookMovementProjection(
            movement.Id,
            movement.Isbn13,
            movement.Type,
            movement.Quantity,
            movement.OccurredAt,
            movement.VolunteerId,
            movement.AssoEventsId,
            movement.ScanSessionId,
            movement.ReversalOfMovementId);

    public static readonly Expression<Func<BookMovement, StatisticsCatalogueMovementProjection>> CatalogueMovement = movement =>
        new StatisticsCatalogueMovementProjection(
            movement.Id,
            movement.Isbn13,
            movement.Type,
            movement.Quantity,
            movement.OccurredAt,
            movement.ReversalOfMovementId);

    public static readonly Expression<Func<Book, StatisticsBookProjection>> PersonalVolunteerBook = book =>
        new StatisticsBookProjection(book.Id, book.Title, null, book.Genre);

    public static readonly Expression<Func<Book, StatisticsBookProjection>> CatalogueBook = book =>
        new StatisticsBookProjection(book.Id, null, null, book.Genre);

    public static readonly Expression<Func<Book, StatisticsBookProjection>> AdminVolunteerBook = book =>
        new StatisticsBookProjection(book.Id, null, null, book.Genre);

    public static readonly Expression<Func<Book, StatisticsBookProjection>> FairBook = book =>
        new StatisticsBookProjection(book.Id, book.Title, book.Authors, book.Genre);

    public static readonly Expression<Func<ScanSession, StatisticsScanSessionProjection>> ScanSession = session =>
        new StatisticsScanSessionProjection(
            session.Id,
            session.VolunteerId,
            session.Mode,
            session.StartedAt,
            session.LastScanAt,
            session.EndedAt,
            session.ScannedCount,
            session.KeptCount,
            session.RejectedCount);

    public static readonly Expression<Func<ScanSession, StatisticsAdminScanSessionProjection>> AdminScanSession = session =>
        new StatisticsAdminScanSessionProjection(
            session.Id,
            session.VolunteerId,
            session.StartedAt,
            session.LastScanAt,
            session.EndedAt,
            session.ScannedCount,
            session.KeptCount,
            session.RejectedCount);
}
