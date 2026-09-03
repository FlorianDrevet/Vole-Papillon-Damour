using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Domain.Common.Errors;

namespace Vole_Papillon_Damour.Application.Books.Commands.ScanSession;

public sealed class CloseScanSessionCommandHandler(
    IProjectDbContext dbContext,
    IDateTimeProvider dateTimeProvider,
    IBookAlertOutbox bookAlertOutbox)
    : IRequestHandler<CloseScanSessionCommand, ErrorOr<ScanSessionResult>>
{
    public async Task<ErrorOr<ScanSessionResult>> Handle(
        CloseScanSessionCommand command,
        CancellationToken cancellationToken)
    {
        var endedAt = dateTimeProvider.UtcNow;
        if (endedAt.Kind != DateTimeKind.Utc)
        {
            return Errors.Book.InvalidScanTimestamp();
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var session = await dbContext.ScanSessions
            .SingleOrDefaultAsync(
                candidate => candidate.Id == command.ScanSessionId,
                cancellationToken);

        if (session is null)
        {
            return Errors.Book.ScanSessionNotFound(command.ScanSessionId.Value);
        }

        if (session.Close(command.CloseReason, endedAt))
        {
            await bookAlertOutbox.QueueForSessionAsync(
                session.Id,
                endedAt,
                cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return ScanSessionResult.From(session);
    }
}
