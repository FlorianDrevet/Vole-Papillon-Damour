using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Domain.Common.Errors;
using Vole_Papillon_Damour.Domain.ManualRequestAggregate;

namespace Vole_Papillon_Damour.Application.ManualRequests.Commands.CreateRequester;

public sealed class CreateManualRequesterCommandHandler(
    IProjectDbContext dbContext,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<CreateManualRequesterCommand, ErrorOr<Guid>>
{
    public async Task<ErrorOr<Guid>> Handle(
        CreateManualRequesterCommand command,
        CancellationToken cancellationToken)
    {
        if (!command.ConsentGiven)
        {
            return Errors.ManualRequest.ConsentRequired();
        }

        if (command.By is null || command.By.Value == Guid.Empty)
        {
            return Errors.ManualRequest.InvalidAuthor();
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

        ManualRequester requester;
        try
        {
            requester = ManualRequester.Create(
                Guid.NewGuid(),
                command.LastName,
                command.FirstName,
                normalizedPhone,
                consentGiven: true,
                command.By,
                now);
        }
        catch (ArgumentException)
        {
            return Errors.ManualRequest.InvalidName();
        }

        dbContext.ManualRequesters.Add(requester);
        await dbContext.SaveChangesAsync(cancellationToken);
        return requester.Id;
    }
}
