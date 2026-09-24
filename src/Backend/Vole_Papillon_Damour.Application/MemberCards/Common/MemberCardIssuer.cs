using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Domain.MemberCardAggregate;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.MemberCards.Common;

public sealed class MemberCardIssuer(IRandomIndexSource randomIndexSource)
{
    private const int MaxRecoveryCodeAttempts = 5;

    public async Task<MemberCard> GetOrIssueAsync(
        IProjectDbContext dbContext,
        UserId userId,
        DateTime issuedAt,
        CancellationToken cancellationToken)
    {
        var existing = await dbContext.MemberCards
            .SingleOrDefaultAsync(card => card.UserId == userId, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var recoveryCode = await GetUniqueRecoveryCodeAsync(dbContext, excludedCardId: null, cancellationToken);
        var card = MemberCard.Issue(Guid.NewGuid(), userId, recoveryCode, issuedAt);
        dbContext.MemberCards.Add(card);
        return card;
    }

    public async Task<string> GetUniqueRecoveryCodeAsync(
        IProjectDbContext dbContext,
        Guid? excludedCardId,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < MaxRecoveryCodeAttempts; attempt++)
        {
            var code = MemberCardRecoveryCode.Generate(randomIndexSource.Next);
            var isTrackedDuplicate = dbContext.MemberCards.Local.Any(card =>
                card.Id != excludedCardId && card.RecoveryCode == code && card.RevokedAt is null);
            if (isTrackedDuplicate)
            {
                continue;
            }

            var isPersistedDuplicate = await dbContext.MemberCards
                .AnyAsync(card => card.Id != excludedCardId &&
                                  card.RecoveryCode == code &&
                                  card.RevokedAt == null,
                    cancellationToken);
            if (!isPersistedDuplicate)
            {
                return code;
            }
        }

        throw new InvalidOperationException("A unique member card recovery code could not be generated after five attempts.");
    }
}
