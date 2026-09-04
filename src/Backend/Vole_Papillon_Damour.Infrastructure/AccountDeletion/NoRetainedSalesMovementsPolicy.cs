using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Domain.UserAggregate.ValueObjects;
using Vole_Papillon_Damour.Infrastructure.Persistence;

namespace Vole_Papillon_Damour.Infrastructure.AccountDeletion;

public sealed class NoRetainedSalesMovementsPolicy(ProjectDbContext dbContext) : IUserDeletionRetentionPolicy
{
    public async Task<bool> HasRetainedSalesMovementsAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var retainedUserId = UserId.Create(userId);

        // These relationships are intentionally restrictive: the user must be
        // anonymized while the historical rows remain queryable.
        return await dbContext.BookMovements
                   .AsNoTracking()
                   .AnyAsync(movement => movement.VolunteerId == retainedUserId, cancellationToken)
               || await dbContext.ScanSessions
                   .AsNoTracking()
                   .AnyAsync(session => session.VolunteerId == retainedUserId, cancellationToken)
               || await dbContext.AssociationSettings
                   .AsNoTracking()
                   .AnyAsync(settings => settings.UpdatedBy == retainedUserId, cancellationToken);
    }
}
