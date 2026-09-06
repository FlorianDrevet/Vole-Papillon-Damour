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
using Vole_Papillon_Damour.Domain.WatchlistAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Books.Commands.Admin;

public sealed class MergeBooksCommandHandler(
    IProjectDbContext dbContext,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<MergeBooksCommand, ErrorOr<AdminBookOperationResult>>
{
    public async Task<ErrorOr<AdminBookOperationResult>> Handle(
        MergeBooksCommand command,
        CancellationToken cancellationToken)
    {
        if (!Isbn13.TryCreate(command.SourceIsbn, out var sourceIsbn) ||
            !Isbn13.TryCreate(command.TargetIsbn, out var targetIsbn))
        {
            return Errors.Book.InvalidIsbn(
                !Isbn13.TryCreate(command.SourceIsbn, out _) ? command.SourceIsbn : command.TargetIsbn);
        }

        if (sourceIsbn == targetIsbn)
        {
            return Errors.Book.CannotMergeBook("The source and target ISBN must be different.");
        }

        if (string.IsNullOrWhiteSpace(command.Note) || command.Note.Trim().Length > 500)
        {
            return Errors.Book.InvalidCorrectionNote();
        }

        if (command.UpdatedBy is null || command.UpdatedBy.Value == Guid.Empty)
        {
            return Error.Validation("Book.InvalidUpdatedBy", "An updating user identifier is required.");
        }

        var mergedAt = dateTimeProvider.UtcNow;
        if (mergedAt.Kind != DateTimeKind.Utc)
        {
            return Errors.Book.InvalidScanTimestamp();
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable,
            cancellationToken);
        var source = await dbContext.Books.SingleOrDefaultAsync(
            candidate => candidate.Id == sourceIsbn,
            cancellationToken);
        var target = await dbContext.Books.SingleOrDefaultAsync(
            candidate => candidate.Id == targetIsbn,
            cancellationToken);
        if (source is null)
        {
            return Errors.Book.NotFound(sourceIsbn.Value);
        }

        if (target is null)
        {
            return Errors.Book.NotFound(targetIsbn.Value);
        }

        if (source.RedirectedToIsbn13 is not null || target.RedirectedToIsbn13 is not null)
        {
            return Errors.Book.CannotMergeBook("A redirected fiche cannot be used in another merge.");
        }

        var activeAnnouncements = await dbContext.BookAnnouncements.AnyAsync(
            announcement =>
                announcement.Isbn13 == sourceIsbn &&
                announcement.Status == BookAnnouncementStatus.Announced,
            cancellationToken);
        if (activeAnnouncements)
        {
            return Errors.Book.CannotMergeBook(
                "The source fiche has active announcements; correct or release them before merging.");
        }

        var sourceQuantity = source.QuantityAvailable;
        if (sourceQuantity > 0)
        {
            source.ApplyQuantityCorrection(0, mergedAt);
            dbContext.BookMovements.Add(BookMovement.Create(
                BookMovementId.CreateUnique(),
                sourceIsbn,
                BookMovementType.Correction,
                -sourceQuantity,
                mergedAt,
                mergedAt,
                clockSuspect: false,
                scanSessionId: null,
                command.UpdatedBy,
                assoEventsId: null,
                $"Merge.Source: {command.Note.Trim()}",
                clientGestureId: null));

            target.RecordAvailableEntry(mergedAt, sourceQuantity);
            dbContext.BookMovements.Add(BookMovement.Create(
                BookMovementId.CreateUnique(),
                targetIsbn,
                BookMovementType.Correction,
                sourceQuantity,
                mergedAt,
                mergedAt,
                clockSuspect: false,
                scanSessionId: null,
                command.UpdatedBy,
                assoEventsId: null,
                $"Merge.Target: {command.Note.Trim()}",
                clientGestureId: null));
        }

        source.RedirectTo(targetIsbn);

        var sourceWatchlistItems = await dbContext.WatchlistItems
            .Where(item => item.Scope == WatchlistItemScope.Edition && item.Isbn13 == sourceIsbn)
            .ToListAsync(cancellationToken);
        var targetWatchlistItems = await dbContext.WatchlistItems
            .Where(item => item.Scope == WatchlistItemScope.Edition && item.Isbn13 == targetIsbn)
            .ToListAsync(cancellationToken);
        foreach (var item in sourceWatchlistItems)
        {
            if (targetWatchlistItems.Any(targetItem => targetItem.UserId == item.UserId))
            {
                dbContext.WatchlistItems.Remove(item);
            }
            else
            {
                item.RedirectEdition(targetIsbn);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var announcedQuantity = await dbContext.BookAnnouncements
            .Where(announcement =>
                announcement.Isbn13 == targetIsbn &&
                announcement.Status == BookAnnouncementStatus.Announced)
            .Select(announcement => (int?)announcement.Quantity)
            .SumAsync(cancellationToken) ?? 0;
        return new AdminBookOperationResult(
            targetIsbn.Value,
            target.QuantityAvailable,
            announcedQuantity,
            Changed: true);
    }
}
