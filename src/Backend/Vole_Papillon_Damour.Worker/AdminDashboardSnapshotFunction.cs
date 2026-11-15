using System.Diagnostics;
using MediatR;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Vole_Papillon_Damour.Application.Books.Commands.Admin;

namespace Vole_Papillon_Damour.Worker;

public sealed class AdminDashboardSnapshotFunction(
    IServiceScopeFactory scopeFactory,
    ILogger<AdminDashboardSnapshotFunction> logger)
{
    [Function("RefreshAdminDashboardSnapshot")]
    public async Task Run(
        [TimerTrigger("0 */10 * * * *")] TimerInfo timer,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        using var scope = scopeFactory.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var result = await sender.Send(new RefreshAdminDashboardSnapshotCommand(), cancellationToken);
        stopwatch.Stop();

        if (result.IsError)
        {
            logger.LogError(
                "Admin dashboard snapshot refresh failed after {DurationMs} ms: {Errors}",
                stopwatch.Elapsed.TotalMilliseconds,
                string.Join("; ", result.Errors.Select(error => error.Description)));
            return;
        }

        logger.LogInformation(
            "Admin dashboard snapshot refreshed. DeadStockCount: {DeadStockCount}, DurationMs: {DurationMs}",
            result.Value.DeadStockCount,
            stopwatch.Elapsed.TotalMilliseconds);
    }
}
