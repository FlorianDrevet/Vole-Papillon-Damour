using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Domain.BookAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate.Entities;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.BookMovementAggregate;
using Vole_Papillon_Damour.Domain.BookMovementAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.Common.Errors;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate.ValueObjects;
using ScanSessionAggregate = Vole_Papillon_Damour.Domain.ScanSessionAggregate.ScanSession;
using AssociationSettingsEntity = Vole_Papillon_Damour.Domain.AssociationSettingsAggregate.AssociationSettings;

namespace Vole_Papillon_Damour.Application.Books.Commands.ScanBook;

public sealed class ScanBookCommandHandler(
    IProjectDbContext dbContext,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<ScanBookCommand, ErrorOr<ScanBookResult>>
{
    private static readonly TimeSpan MaximumFutureSkew = TimeSpan.Zero;

    public async Task<ErrorOr<ScanBookResult>> Handle(
        ScanBookCommand command,
        CancellationToken cancellationToken)
    {
        if (!Isbn13.TryCreate(command.Isbn, out var isbn13))
        {
            return Errors.Book.InvalidIsbn(command.Isbn);
        }

        if (command.ClientGestureId == Guid.Empty ||
            command.OccurredAt.Kind != DateTimeKind.Utc)
        {
            return command.OccurredAt.Kind != DateTimeKind.Utc
                ? Errors.Book.InvalidScanTimestamp()
                : Error.Validation("Book.InvalidClientGestureId", "A client gesture identifier is required.");
        }

        var receivedAt = dateTimeProvider.UtcNow;
        if (receivedAt.Kind != DateTimeKind.Utc)
        {
            return Errors.Book.InvalidScanTimestamp();
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var existingMovement = await dbContext.BookMovements
            .SingleOrDefaultAsync(
                movement => movement.ClientGestureId == command.ClientGestureId,
                cancellationToken);

        if (existingMovement is not null)
        {
            var existingResult = await BuildExistingResultAsync(
                existingMovement,
                cancellationToken);

            if (existingResult.IsError)
            {
                return existingResult.Errors;
            }

            await transaction.CommitAsync(cancellationToken);
            return existingResult.Value;
        }

        var session = await dbContext.ScanSessions
            .SingleOrDefaultAsync(
                candidate => candidate.Id == command.ScanSessionId,
                cancellationToken);

        if (session is null)
        {
            return Errors.Book.ScanSessionNotFound(command.ScanSessionId.Value);
        }

        if (session.Status != ScanSessionStatus.InProgress)
        {
            return Errors.Book.ScanSessionClosed(command.ScanSessionId.Value);
        }

        if (session.Mode == ScanMode.NextFair && session.TargetAssoEventsId is { } targetFairId)
        {
            // A fair may be cancelled after the session was opened. Recheck the
            // target on the hot path so a late scan cannot create a stale
            // announcement. Keep the missing-target behavior for legacy
            // sessions that intentionally use the undated flow.
            var targetFair = await dbContext.AssoEvents
                .SingleOrDefaultAsync(
                    assoEvent => assoEvent.Id == targetFairId,
                    cancellationToken);
            if (targetFair?.IsCancelled == true)
            {
                return Errors.Book.FairCancelled(targetFairId.Value);
            }

            if (targetFair?.EventsType?.Value is not null &&
                targetFair.EventsType.Value !=
                Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects.EventsType.EventsTypeEnum.Books)
            {
                return Errors.Book.TargetFairMustBeBooks();
            }
        }

        var book = await dbContext.Books
            .SingleOrDefaultAsync(candidate => candidate.Id == isbn13, cancellationToken);

        if (book?.RedirectedToIsbn13 is { } canonicalIsbn13)
        {
            isbn13 = canonicalIsbn13;
            book = await dbContext.Books
                .SingleOrDefaultAsync(candidate => candidate.Id == isbn13, cancellationToken);

            if (book is null)
            {
                return Errors.Book.RedirectTargetNotFound(isbn13.Value);
            }
        }

        if (book is null)
        {
            book = Book.Create(isbn13, receivedAt);
            dbContext.Books.Add(book);
        }

        var settings = await GetOrCreateSettingsAsync(session, receivedAt, cancellationToken);
        var quantityAnnounced = await GetQuantityAnnouncedAsync(isbn13, cancellationToken);
        var decision = CalculateVerdict(book, quantityAnnounced, settings);
        var (occurredAt, clockSuspect) = NormalizeClientTimestamp(
            command.OccurredAt,
            session.StartedAt,
            receivedAt);

        var movementType = BookMovementType.Rejection;
        BookAnnouncement? announcement = null;

        if (command.Kept)
        {
            if (session.Mode == ScanMode.AvailableNow)
            {
                book.RecordAvailableEntry(occurredAt);
                movementType = BookMovementType.DirectEntry;
            }
            else
            {
                book.RecordAnnouncementEntry(occurredAt);
                movementType = BookMovementType.AnnouncementEntry;
                announcement = BookAnnouncement.Create(
                    BookAnnouncementId.CreateUnique(),
                    isbn13,
                    session.TargetAssoEventsId,
                    quantity: 1,
                    occurredAt,
                    session.Id,
                    command.ClientGestureId);
                dbContext.BookAnnouncements.Add(announcement);
            }
        }
        else
        {
            book.RecordRejection(occurredAt);
        }

        session.RecordScan(command.Kept, occurredAt, receivedAt);

        var movement = BookMovement.Create(
            BookMovementId.CreateUnique(),
            isbn13,
            movementType,
            quantity: 1,
            occurredAt,
            receivedAt,
            clockSuspect,
            session.Id,
            session.VolunteerId,
            session.TargetAssoEventsId,
            note: null,
            command.ClientGestureId);
        dbContext.BookMovements.Add(movement);

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var finalQuantityAnnounced = quantityAnnounced + (announcement is null ? 0 : 1);
        return new ScanBookResult(
            isbn13.Value,
            decision,
            book.QuantityAvailable,
            finalQuantityAnnounced,
            session.Id,
            movementType,
            AlreadyProcessed: false,
            clockSuspect);
    }

    private async Task<ErrorOr<ScanBookResult>> BuildExistingResultAsync(
        BookMovement movement,
        CancellationToken cancellationToken)
    {
        var book = await dbContext.Books
            .SingleOrDefaultAsync(candidate => candidate.Id == movement.Isbn13, cancellationToken);

        if (book is null)
        {
            return Error.Unexpected(
                "Book.MovementWithoutBook",
                "The idempotent movement points to a missing book.");
        }

        var settings = await dbContext.AssociationSettings
            .SingleOrDefaultAsync(
                candidate => candidate.Id == AssociationSettingsEntity.SingletonId,
                cancellationToken);
        var quantityAnnounced = await GetQuantityAnnouncedAsync(movement.Isbn13, cancellationToken);
        var decision = CalculateVerdict(
            book,
            quantityAnnounced,
            settings ?? CreateDefaultSettingsForCalculation());
        var sessionId = movement.ScanSessionId ?? ScanSessionId.Create(Guid.Empty);

        return new ScanBookResult(
            movement.Isbn13.Value,
            decision,
            book.QuantityAvailable,
            quantityAnnounced,
            sessionId,
            movement.Type,
            AlreadyProcessed: true,
            movement.ClockSuspect);
    }

    private async Task<AssociationSettingsEntity> GetOrCreateSettingsAsync(
        ScanSessionAggregate session,
        DateTime receivedAt,
        CancellationToken cancellationToken)
    {
        var settings = await dbContext.AssociationSettings
            .SingleOrDefaultAsync(
                candidate => candidate.Id == AssociationSettingsEntity.SingletonId,
                cancellationToken);

        if (settings is not null)
        {
            return settings;
        }

        settings = AssociationSettingsEntity.Create(session.VolunteerId, receivedAt);
        dbContext.AssociationSettings.Add(settings);
        return settings;
    }

    private async Task<int> GetQuantityAnnouncedAsync(
        Isbn13 isbn13,
        CancellationToken cancellationToken)
    {
        return await dbContext.BookAnnouncements
            .Where(announcement =>
                announcement.Isbn13 == isbn13 &&
                announcement.Status == BookAnnouncementStatus.Announced)
            .Select(announcement => (int?)announcement.Quantity)
            .SumAsync(cancellationToken) ?? 0;
    }

    private static BookVerdictDecision CalculateVerdict(
        Book book,
        int quantityAnnounced,
        AssociationSettingsEntity settings)
    {
        return BookVerdictCalculator.Calculate(
            new BookVerdictFacts(
                book.QuantityAvailable,
                quantityAnnounced,
                book.SalesCount,
                ActiveRequesterCount: 0,
                book.IsRare),
            settings.DuplicateThreshold,
            settings.DemandSalesThreshold);
    }

    private static (DateTime OccurredAt, bool ClockSuspect) NormalizeClientTimestamp(
        DateTime clientTimestamp,
        DateTime sessionStartedAt,
        DateTime receivedAt)
    {
        var clockSuspect = clientTimestamp < sessionStartedAt ||
                           clientTimestamp > receivedAt.Add(MaximumFutureSkew);
        return clockSuspect
            ? (receivedAt, true)
            : (clientTimestamp, false);
    }

    private static AssociationSettingsEntity CreateDefaultSettingsForCalculation()
    {
        return AssociationSettingsEntity.Create(
            Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects.UserId.Create(Guid.Empty),
            new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc));
    }
}
