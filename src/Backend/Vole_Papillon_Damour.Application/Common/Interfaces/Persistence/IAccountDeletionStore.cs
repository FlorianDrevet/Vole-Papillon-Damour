using Vole_Papillon_Damour.Application.Common.Models;

namespace Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;

public interface IAccountDeletionStore
{
    Task<AccountDeletionWorkItem> EnsurePendingAsync(
        string externalId,
        DateTime requestedAt,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<AccountDeletionWorkItem>> ClaimPendingAsync(
        DateTime now,
        TimeSpan lease,
        int batchSize,
        CancellationToken cancellationToken);

    Task FinalizeAsync(
        AccountDeletionWorkItem workItem,
        DateTime completedAt,
        CancellationToken cancellationToken);

    Task RecordFailureAsync(
        Guid requestId,
        string failureCode,
        DateTime failedAt,
        CancellationToken cancellationToken);
}
