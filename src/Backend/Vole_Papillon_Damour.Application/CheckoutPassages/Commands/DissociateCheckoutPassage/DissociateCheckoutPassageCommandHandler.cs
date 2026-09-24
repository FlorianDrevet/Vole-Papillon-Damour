using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Domain.CheckoutPassageAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.Common.Errors;

namespace Vole_Papillon_Damour.Application.CheckoutPassages.Commands.DissociateCheckoutPassage;

public sealed class DissociateCheckoutPassageCommandHandler(
    IProjectDbContext dbContext,
    IDateTimeProvider dateTimeProvider,
    ILogger<DissociateCheckoutPassageCommandHandler> logger)
    : IRequestHandler<DissociateCheckoutPassageCommand, ErrorOr<Success>>
{
    public async Task<ErrorOr<Success>> Handle(
        DissociateCheckoutPassageCommand command,
        CancellationToken cancellationToken)
    {
        if (command.CheckoutPassageId == Guid.Empty)
        {
            return Errors.CheckoutPassage.InvalidId();
        }

        var passage = await dbContext.CheckoutPassages.SingleOrDefaultAsync(
            candidate => candidate.Id == command.CheckoutPassageId,
            cancellationToken);
        if (passage is null)
        {
            return Errors.CheckoutPassage.NotFound(command.CheckoutPassageId);
        }

        if (passage.Status == CheckoutPassageStatus.Dissociated)
        {
            return Result.Success;
        }

        if (passage.Status != CheckoutPassageStatus.Associated)
        {
            return Errors.CheckoutPassage.NotAssociated();
        }

        var reason = command.Reason.Trim();
        passage.Dissociate(command.AdministratorId, dateTimeProvider.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "CheckoutPassageDissociated {PassageId} {AdministratorId} {Reason}",
            passage.Id,
            command.AdministratorId.Value,
            reason);

        return Result.Success;
    }
}
