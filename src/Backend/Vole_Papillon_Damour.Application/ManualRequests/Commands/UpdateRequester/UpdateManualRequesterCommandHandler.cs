using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Domain.Common.Errors;
using Vole_Papillon_Damour.Domain.ManualRequestAggregate;

namespace Vole_Papillon_Damour.Application.ManualRequests.Commands.UpdateRequester;

public sealed class UpdateManualRequesterCommandHandler(
    IProjectDbContext dbContext,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<UpdateManualRequesterCommand, ErrorOr<Success>>
{
    public async Task<ErrorOr<Success>> Handle(
        UpdateManualRequesterCommand command,
        CancellationToken cancellationToken)
    {
        if (command.By is null || command.By.Value == Guid.Empty)
        {
            return Errors.ManualRequest.InvalidAuthor();
        }

        var requester = await dbContext.ManualRequesters
            .SingleOrDefaultAsync(candidate => candidate.Id == command.RequesterId, cancellationToken);
        if (requester is null)
        {
            return Errors.ManualRequest.RequesterNotFound(command.RequesterId);
        }

        if (requester.IsErased)
        {
            return Errors.ManualRequest.RequesterErased();
        }

        var now = dateTimeProvider.UtcNow;
        if (now.Kind != DateTimeKind.Utc)
        {
            return Error.Validation("ManualRequest.InvalidClock", "The requester timestamp must be expressed in UTC.");
        }

        string normalizedPhone;
        try
        {
            normalizedPhone = ManualRequester.NormalizePhone(command.Phone);
        }
        catch (ArgumentException)
        {
            return Errors.ManualRequest.InvalidPhone();
        }

        try
        {
            requester.UpdateContact(command.LastName, command.FirstName, normalizedPhone, now);
        }
        catch (ArgumentException)
        {
            return Errors.ManualRequest.InvalidName();
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success;
    }
}
