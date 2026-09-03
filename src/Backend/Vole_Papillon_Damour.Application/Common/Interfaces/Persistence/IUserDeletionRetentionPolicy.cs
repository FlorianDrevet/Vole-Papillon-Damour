namespace Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;

public interface IUserDeletionRetentionPolicy
{
    Task<bool> HasRetainedSalesMovementsAsync(Guid userId, CancellationToken cancellationToken);
}
