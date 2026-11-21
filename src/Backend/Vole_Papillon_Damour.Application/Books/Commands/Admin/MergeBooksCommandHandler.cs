using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.NotFoundReports.Common;
using Vole_Papillon_Damour.Domain.BookAggregate;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.Common.Errors;

namespace Vole_Papillon_Damour.Application.Books.Commands.Admin;

public sealed class MergeBooksCommandHandler(
    IProjectDbContext dbContext,
    IDateTimeProvider dateTimeProvider,
    INotFoundReportLapser notFoundReportLapser)
    : IRequestHandler<MergeBooksCommand, ErrorOr<AdminBookOperationResult>>
{
    public async Task<ErrorOr<AdminBookOperationResult>> Handle(
        MergeBooksCommand command,
        CancellationToken cancellationToken)
    {
        if (!BookCode.TryCreate(command.SourceIsbn, out var sourceIsbn) ||
            !BookCode.TryCreate(command.TargetIsbn, out var targetCode))
        {
            return Errors.Book.InvalidIsbn(
                !BookCode.TryCreate(command.SourceIsbn, out _) ? command.SourceIsbn : command.TargetIsbn);
        }

        // Une fusion redirige vers une édition réelle : la cible doit être un ISBN, pas un code interne.
        if (!targetCode.TryGetIsbn13(out var targetIsbn))
        {
            return Errors.Book.InvalidIsbn(command.TargetIsbn);
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

        var merged = await BookMergeOperation.ApplyAsync(
            dbContext,
            notFoundReportLapser,
            source,
            target,
            command.Note.Trim(),
            command.UpdatedBy,
            mergedAt,
            cancellationToken);
        if (merged.IsError)
        {
            return merged.Errors;
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
