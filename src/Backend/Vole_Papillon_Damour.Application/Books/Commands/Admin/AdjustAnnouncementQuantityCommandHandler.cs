using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.BookMovementAggregate;
using Vole_Papillon_Damour.Domain.BookMovementAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.Common.Errors;

namespace Vole_Papillon_Damour.Application.Books.Commands.Admin;

public sealed class AdjustAnnouncementQuantityCommandHandler(
    IProjectDbContext dbContext,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<AdjustAnnouncementQuantityCommand, ErrorOr<AdminBookOperationResult>>
{
    public async Task<ErrorOr<AdminBookOperationResult>> Handle(
        AdjustAnnouncementQuantityCommand command,
        CancellationToken cancellationToken)
    {
        if (command.AnnouncementId == Guid.Empty || command.Quantity <= 0)
        {
            return Errors.Book.InvalidCorrectionQuantity();
        }

        if (string.IsNullOrWhiteSpace(command.Note) || command.Note.Trim().Length > 500)
        {
            return Errors.Book.InvalidCorrectionNote();
        }

        if (command.UpdatedBy is null || command.UpdatedBy.Value == Guid.Empty)
        {
            return Error.Validation("Book.InvalidUpdatedBy", "An updating user identifier is required.");
        }

        var updatedAt = dateTimeProvider.UtcNow;
        if (updatedAt.Kind != DateTimeKind.Utc)
        {
            return Errors.Book.InvalidScanTimestamp();
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var announcement = await dbContext.BookAnnouncements.SingleOrDefaultAsync(
            candidate => candidate.Id == BookAnnouncementId.Create(command.AnnouncementId),
            cancellationToken);
        if (announcement is null)
        {
            return Errors.Book.AnnouncementNotFound(command.AnnouncementId);
        }

        if (announcement.Status != BookAnnouncementStatus.Announced)
        {
            return Errors.Book.AnnouncementAlreadyReleasedForCorrection(command.AnnouncementId);
        }

        var book = await dbContext.Books.SingleOrDefaultAsync(
            candidate => candidate.Id == announcement.Isbn13,
            cancellationToken);
        if (book?.RedirectedToIsbn13 is { } canonicalIsbn13)
        {
            book = await dbContext.Books.SingleOrDefaultAsync(
                candidate => candidate.Id == canonicalIsbn13,
                cancellationToken);
        }

        if (book is null)
        {
            return Errors.Book.NotFound(announcement.Isbn13.Value);
        }

        var delta = announcement.ApplyQuantityCorrection(command.Quantity);
        Guid? movementId = null;
        if (delta != 0)
        {
            var movement = BookMovement.Create(
                BookMovementId.CreateUnique(),
                announcement.Isbn13,
                BookMovementType.Correction,
                delta,
                updatedAt,
                updatedAt,
                clockSuspect: false,
                scanSessionId: null,
                command.UpdatedBy,
                announcement.AssoEventsId,
                $"Announcement.Correction: {command.Note.Trim()}",
                clientGestureId: null);
            movementId = movement.Id.Value;
            dbContext.BookMovements.Add(movement);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var announcedQuantity = await dbContext.BookAnnouncements
            .Where(candidate =>
                candidate.Isbn13 == announcement.Isbn13 &&
                candidate.Status == BookAnnouncementStatus.Announced)
            .Select(candidate => (int?)candidate.Quantity)
            .SumAsync(cancellationToken) ?? 0;
        return new AdminBookOperationResult(
            book.Isbn13.Value,
            book.QuantityAvailable,
            announcedQuantity,
            delta != 0,
            movementId);
    }
}
