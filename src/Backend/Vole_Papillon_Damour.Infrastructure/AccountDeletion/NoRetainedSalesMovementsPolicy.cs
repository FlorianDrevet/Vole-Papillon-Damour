using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;

namespace Vole_Papillon_Damour.Infrastructure.AccountDeletion;

public sealed class NoRetainedSalesMovementsPolicy : IUserDeletionRetentionPolicy
{
    public Task<bool> HasRetainedSalesMovementsAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        // Book movements are introduced by P1-3. Until that table exists, every
        // current User row is removable rather than anonymized.
        return Task.FromResult(false);
    }
}
