using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;

namespace Vole_Papillon_Damour.Worker;

public sealed class AccountDeletionSweepFunction(
    IServiceScopeFactory scopeFactory,
    ILogger<AccountDeletionSweepFunction> logger)
{
    [Function(nameof(AccountDeletionSweepFunction))]
    public async Task Run(
        [TimerTrigger("0 */5 * * * *")] TimerInfo timer,
        CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var accountDeletionService = scope.ServiceProvider.GetRequiredService<IAccountDeletionService>();
        var completedCount = await accountDeletionService.ProcessPendingAsync(cancellationToken);

        logger.LogInformation(
            "Account deletion sweep completed. CompletedCount: {CompletedCount}",
            completedCount);
    }
}
