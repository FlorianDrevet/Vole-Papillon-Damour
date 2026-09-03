using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Domain.Common.Errors;

namespace Vole_Papillon_Damour.Application.Books.Commands.ForceBookAlerts;

public sealed class ForceBookAlertsCommandHandler(
    IProjectDbContext dbContext,
    IBookAlertOutbox bookAlertOutbox,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<ForceBookAlertsCommand, ErrorOr<BookAlertOperationResult>>
{
    public async Task<ErrorOr<BookAlertOperationResult>> Handle(
        ForceBookAlertsCommand command,
        CancellationToken cancellationToken)
    {
        if (command.ScanSessionId is null)
        {
            return Errors.Book.ScanSessionNotFound(Guid.Empty);
        }

        if (command.UpdatedBy is null || command.UpdatedBy.Value == Guid.Empty)
        {
            return Error.Validation("Book.InvalidUpdatedBy", "An updating user identifier is required.");
        }

        var forcedAt = dateTimeProvider.UtcNow;
        if (forcedAt.Kind != DateTimeKind.Utc)
        {
            return Errors.Book.InvalidScanTimestamp();
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var sessionExists = await dbContext.ScanSessions
            .AnyAsync(
                session => session.Id == command.ScanSessionId,
                cancellationToken);
        if (!sessionExists)
        {
            return Errors.Book.ScanSessionNotFound(command.ScanSessionId.Value);
        }

        var affectedCount = await bookAlertOutbox.ForcePendingForSessionAsync(
            command.ScanSessionId,
            forcedAt,
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new BookAlertOperationResult(command.ScanSessionId, affectedCount);
    }
}
