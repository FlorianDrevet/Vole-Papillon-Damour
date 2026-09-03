using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.Common.Models;

namespace Vole_Papillon_Damour.Application.AccountDeletion;

public sealed class AccountDeletionService(
    IAccountDeletionStore store,
    IEntraUserDirectory directory,
    IDateTimeProvider clock) : IAccountDeletionService
{
    private const int ClaimBatchSize = 50;
    private static readonly TimeSpan ClaimLease = TimeSpan.FromMinutes(5);

    public async Task<AccountDeletionRequestResult> RequestAsync(
        string externalId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(externalId);

        var workItem = await store.EnsurePendingAsync(
            externalId,
            clock.UtcNow,
            cancellationToken);

        var completed = await ProcessAsync(workItem, cancellationToken);
        return new AccountDeletionRequestResult(completed);
    }

    public async Task<int> ProcessPendingAsync(CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var workItems = await store.ClaimPendingAsync(
            now,
            ClaimLease,
            ClaimBatchSize,
            cancellationToken);

        var completedCount = 0;
        foreach (var workItem in workItems)
        {
            if (await ProcessAsync(workItem, cancellationToken))
            {
                completedCount++;
            }
        }

        return completedCount;
    }

    private async Task<bool> ProcessAsync(
        AccountDeletionWorkItem workItem,
        CancellationToken cancellationToken)
    {
        try
        {
            await directory.DeleteAsync(workItem.ExternalId, cancellationToken);
            await store.FinalizeAsync(workItem, clock.UtcNow, cancellationToken);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (AccountDeletionDependencyException exception)
        {
            await RecordFailureAsync(workItem, exception.FailureCode, cancellationToken);
            return false;
        }
        catch (Exception)
        {
            // A database failure after a successful Graph delete is also replayable:
            // the next attempt receives Graph's idempotent 404 and finalizes locally.
            await RecordFailureAsync(workItem, "unexpected", cancellationToken);
            return false;
        }
    }

    private async Task RecordFailureAsync(
        AccountDeletionWorkItem workItem,
        string failureCode,
        CancellationToken cancellationToken)
    {
        await store.RecordFailureAsync(
            workItem.RequestId,
            failureCode,
            clock.UtcNow,
            cancellationToken);
    }
}
