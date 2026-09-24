using ErrorOr;
using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Domain.Common.Errors;
using Vole_Papillon_Damour.Domain.MemberCardAggregate;

namespace Vole_Papillon_Damour.Application.MemberCards.Queries.ResolveMemberCard;

public static class MemberCardResolver
{
    public static async Task<ErrorOr<ResolvedMemberCard>> ResolveAsync(
        IProjectDbContext dbContext,
        IMemberCardTokenService tokens,
        string credential,
        CancellationToken cancellationToken)
    {
        Domain.MemberCardAggregate.MemberCard? card = null;
        if (tokens.TryRead(credential, out var cardId, out var version))
        {
            card = await dbContext.MemberCards.AsNoTracking()
                .SingleOrDefaultAsync(candidate => candidate.Id == cardId &&
                                                   candidate.Version == version &&
                                                   candidate.RevokedAt == null,
                    cancellationToken);
        }
        else if (MemberCardRecoveryCode.TryNormalize(credential, out var recoveryCode))
        {
            card = await dbContext.MemberCards.AsNoTracking()
                .SingleOrDefaultAsync(candidate => candidate.RecoveryCode == recoveryCode &&
                                                   candidate.RevokedAt == null,
                    cancellationToken);
        }

        if (card is null)
        {
            return Errors.MemberCard.NotRecognised();
        }

        var user = await dbContext.Users.AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == card.UserId && candidate.AnonymizedAt == null,
                cancellationToken);
        if (user is null)
        {
            return Errors.MemberCard.NotRecognised();
        }

        var displayLabel = string.IsNullOrWhiteSpace(user.Name?.FirstName)
            ? "Membre"
            : user.Name!.FirstName.Trim();
        return new ResolvedMemberCard(user.Id, displayLabel);
    }
}
