using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Domain.Common.Errors;
using Vole_Papillon_Damour.Domain.ManualRequestAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.ManualRequests.Commands.MarkGiven;

public sealed class MarkManualRequestGivenCommandHandler(
    IProjectDbContext dbContext,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<MarkManualRequestGivenCommand, ErrorOr<Success>>
{
    public async Task<ErrorOr<Success>> Handle(
        MarkManualRequestGivenCommand command,
        CancellationToken cancellationToken)
    {
        if (command.By is null || command.By.Value == Guid.Empty)
        {
            return Errors.ManualRequest.InvalidAuthor();
        }

        var now = dateTimeProvider.UtcNow;
        if (now.Kind != DateTimeKind.Utc)
        {
            return Error.Validation("ManualRequest.InvalidClock", "The request timestamp must be expressed in UTC.");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var request = await dbContext.ManualBookRequests
            .SingleOrDefaultAsync(candidate => candidate.Id == command.RequestId, cancellationToken);
        if (request is null)
        {
            return Errors.ManualRequest.RequestNotFound(command.RequestId);
        }

        if (request.Status != ManualBookRequestStatus.Found)
        {
            return Errors.ManualRequest.InvalidTransition();
        }

        request.MarkGiven(command.By, now);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result.Success;
    }
}
