using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Domain.Common.Errors;

namespace Vole_Papillon_Damour.Application.Books.Commands.CancelBookAlerts;

public sealed class CancelBookAlertsCommandHandler(
    IProjectDbContext dbContext,
    IBookAlertOutbox bookAlertOutbox)
    : IRequestHandler<CancelBookAlertsCommand, ErrorOr<BookAlertOperationResult>>
{
    public async Task<ErrorOr<BookAlertOperationResult>> Handle(
        CancelBookAlertsCommand command,
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

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var sessionExists = await dbContext.ScanSessions
            .AnyAsync(
                session => session.Id == command.ScanSessionId,
                cancellationToken);
        if (!sessionExists)
        {
            return Errors.Book.ScanSessionNotFound(command.ScanSessionId.Value);
        }

        var affectedCount = await bookAlertOutbox.CancelPendingForSessionAsync(
            command.ScanSessionId,
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new BookAlertOperationResult(command.ScanSessionId, affectedCount);
    }
}
