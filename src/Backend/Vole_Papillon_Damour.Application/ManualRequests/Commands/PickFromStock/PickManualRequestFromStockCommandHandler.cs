using System.Data;
using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.ManualRequests.Common;
using Vole_Papillon_Damour.Domain.Common.Errors;
using Vole_Papillon_Damour.Domain.ManualRequestAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.ManualRequests.Commands.PickFromStock;

public sealed class PickManualRequestFromStockCommandHandler(
    IProjectDbContext dbContext,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<PickManualRequestFromStockCommand, ErrorOr<Success>>
{
    public async Task<ErrorOr<Success>> Handle(
        PickManualRequestFromStockCommand command,
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

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var request = await dbContext.ManualBookRequests
            .SingleOrDefaultAsync(candidate => candidate.Id == command.RequestId, cancellationToken);
        if (request is null)
        {
            return Errors.ManualRequest.RequestNotFound(command.RequestId);
        }

        var result = await ManualRequestStockPick.PickAsync(
            dbContext, request, command.By, now, cancellationToken);
        if (result.IsError)
        {
            return result.Errors;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result.Success;
    }
}
