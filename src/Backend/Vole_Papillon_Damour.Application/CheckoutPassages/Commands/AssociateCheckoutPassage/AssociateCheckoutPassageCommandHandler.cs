using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Vole_Papillon_Damour.Application.CheckoutPassages.Common;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.MemberCards.Queries.ResolveMemberCard;
using Vole_Papillon_Damour.Domain.CheckoutPassageAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.Common.Errors;

namespace Vole_Papillon_Damour.Application.CheckoutPassages.Commands.AssociateCheckoutPassage;

public sealed class AssociateCheckoutPassageCommandHandler(
    IProjectDbContext dbContext,
    IDateTimeProvider dateTimeProvider,
    CheckoutPassageRecorder recorder,
    IMemberCardTokenService tokens,
    ILogger<AssociateCheckoutPassageCommandHandler> logger)
    : IRequestHandler<AssociateCheckoutPassageCommand, ErrorOr<CheckoutPassageAssociationResult>>
{
    public async Task<ErrorOr<CheckoutPassageAssociationResult>> Handle(
        AssociateCheckoutPassageCommand command,
        CancellationToken cancellationToken)
    {
        if (command.CheckoutPassageId == Guid.Empty)
        {
            return Errors.CheckoutPassage.InvalidId();
        }

        var now = dateTimeProvider.UtcNow;
        var occurredAt = command.OccurredAt.Kind == DateTimeKind.Utc && command.OccurredAt <= now
            ? command.OccurredAt
            : now;

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var passage = await recorder.GetOrOpenAsync(command.CheckoutPassageId, occurredAt, cancellationToken);
        var resolved = await MemberCardResolver.ResolveAsync(
            dbContext, tokens, command.Credential, cancellationToken);

        if (resolved.IsError)
        {
            if (passage.Status == CheckoutPassageStatus.PendingAssociation)
            {
                passage.MarkUnresolved("card-not-recognised", now);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            LogAssociation(passage.Id, passage.Status, command.VolunteerId.Value);
            return new CheckoutPassageAssociationResult(
                passage.Id, passage.Status.ToString(), null, AlreadyProcessed: false);
        }

        bool changed;
        try
        {
            changed = passage.Associate(resolved.Value.UserId, command.VolunteerId, now);
        }
        catch (InvalidOperationException)
        {
            return Errors.CheckoutPassage.AlreadyAssociatedToAnotherMember();
        }

        await recorder.MarkSelectionPurchasedAsync(passage, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        LogAssociation(passage.Id, passage.Status, command.VolunteerId.Value);

        return new CheckoutPassageAssociationResult(
            passage.Id,
            passage.Status.ToString(),
            passage.IsVisibleToMember ? resolved.Value.DisplayLabel : null,
            AlreadyProcessed: !changed);
    }

    private void LogAssociation(Guid passageId, CheckoutPassageStatus status, Guid volunteerId) =>
        logger.LogInformation(
            "CheckoutPassageAssociated {PassageId} {Status} {VolunteerId}",
            passageId,
            status,
            volunteerId);
}