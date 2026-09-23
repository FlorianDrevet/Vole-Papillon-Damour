using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.Common.Services;
using Vole_Papillon_Damour.Application.MemberCards.Common;
using Vole_Papillon_Damour.Application.MemberCards.Queries.GetMyCard;
using Vole_Papillon_Damour.Domain.Common.Errors;

namespace Vole_Papillon_Damour.Application.MemberCards.Commands.RotateMyCard;

public sealed class RotateMyCardCommandHandler(
    IProjectDbContext dbContext,
    MemberIdentityService memberIdentityService,
    IDateTimeProvider dateTimeProvider,
    MemberCardIssuer cardIssuer,
    IMemberCardTokenService tokens) : IRequestHandler<RotateMyCardCommand, ErrorOr<MyCardResult>>
{
    public async Task<ErrorOr<MyCardResult>> Handle(RotateMyCardCommand request, CancellationToken cancellationToken)
    {
        var user = await memberIdentityService.EnsureAsync(
            request.ExternalId,
            request.Email,
            request.FirstName,
            request.LastName,
            cancellationToken);
        var card = await cardIssuer.GetOrIssueAsync(dbContext, user.Id, dateTimeProvider.UtcNow, cancellationToken);
        if (!card.IsActive)
        {
            return Errors.MemberCard.NotRecognised();
        }

        var uniqueCode = await cardIssuer.GetUniqueRecoveryCodeAsync(dbContext, card.Id, cancellationToken);
        card.Rotate(uniqueCode, dateTimeProvider.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
        return GetMyCardQueryHandler.ToResult(card, user.Name?.FirstName, tokens);
    }
}
