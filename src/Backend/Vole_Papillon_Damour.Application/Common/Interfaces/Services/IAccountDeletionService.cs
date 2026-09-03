namespace Vole_Papillon_Damour.Application.Common.Interfaces.Services;

public interface IAccountDeletionService
{
    Task<AccountDeletionRequestResult> RequestAsync(
        string externalId,
        CancellationToken cancellationToken);

    Task<int> ProcessPendingAsync(CancellationToken cancellationToken);
}

public sealed record AccountDeletionRequestResult(bool IsCompleted);
