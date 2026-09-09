using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Books.Commands.ScanSession;
using Vole_Papillon_Damour.Application.Books.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Domain.Common.Errors;
using Vole_Papillon_Damour.Domain.ScanSessionAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Books.Commands.Admin;

/// <summary>
/// Resolves the session owner, then delegates the actual transaction and alert
/// queueing to the regular close-session handler. The API policy is the
/// authorization boundary; the administrator identifier is still validated so
/// this command cannot be invoked with an empty audit actor.
/// </summary>
public sealed class ForceCloseScanSessionCommandHandler(
    IProjectDbContext dbContext,
    CloseScanSessionCommandHandler closeScanSessionHandler)
    : IRequestHandler<ForceCloseScanSessionCommand, ErrorOr<ScanSessionResult>>
{
    public async Task<ErrorOr<ScanSessionResult>> Handle(
        ForceCloseScanSessionCommand command,
        CancellationToken cancellationToken)
    {
        if (command.ScanSessionId is null)
        {
            return Errors.Book.ScanSessionNotFound(Guid.Empty);
        }

        if (command.AdministratorId is null || command.AdministratorId.Value == Guid.Empty)
        {
            return Error.Validation("Book.InvalidAdministrator", "An administrator identifier is required.");
        }

        var volunteerId = await dbContext.ScanSessions
            .AsNoTracking()
            .Where(session => session.Id == command.ScanSessionId)
            .Select(session => session.VolunteerId)
            .SingleOrDefaultAsync(cancellationToken);
        if (volunteerId is null)
        {
            return Errors.Book.ScanSessionNotFound(command.ScanSessionId.Value);
        }

        return await closeScanSessionHandler.Handle(
            new CloseScanSessionCommand(
                command.ScanSessionId,
                ScanCloseReason.AdminForced,
                volunteerId),
            cancellationToken);
    }
}
