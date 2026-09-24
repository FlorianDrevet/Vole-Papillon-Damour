using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.NotFoundReports.Common;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.Common.Errors;

namespace Vole_Papillon_Damour.Application.Books.Commands.Admin;

public sealed class WithdrawBookCommandHandler(
    IProjectDbContext dbContext,
    IDateTimeProvider dateTimeProvider,
    INotFoundReportLapser notFoundReportLapser)
    : IRequestHandler<WithdrawBookCommand, ErrorOr<AdminBookOperationResult>>
{
    public async Task<ErrorOr<AdminBookOperationResult>> Handle(
        WithdrawBookCommand command,
        CancellationToken cancellationToken)
    {
        if (!Isbn13.TryCreate(command.Isbn, out _))
        {
            return Errors.Book.InvalidIsbn(command.Isbn);
        }

        if (command.Quantity <= 0)
        {
            return Errors.Book.InvalidWithdrawalQuantity();
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
        var withdrawal = await BookWithdrawal.WithdrawAsync(
            dbContext,
            command.Isbn,
            command.Quantity,
            command.Note,
            command.UpdatedBy,
            updatedAt,
            cancellationToken);
        if (withdrawal.IsError)
        {
            return withdrawal.Errors;
        }

        await notFoundReportLapser.LapseIfUnavailableAsync(
            withdrawal.Value.Isbn13, updatedAt, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        var announcedQuantity = await dbContext.BookAnnouncements
            .Where(announcement =>
                announcement.Isbn13 == withdrawal.Value.Isbn13 &&
                announcement.Status == BookAnnouncementStatus.Announced)
            .Select(announcement => (int?)announcement.Quantity)
            .SumAsync(cancellationToken) ?? 0;
        return new AdminBookOperationResult(
            withdrawal.Value.Isbn13.Value,
            withdrawal.Value.QuantityAvailable,
            announcedQuantity,
            Changed: true,
            withdrawal.Value.MovementId.Value);
    }
}
