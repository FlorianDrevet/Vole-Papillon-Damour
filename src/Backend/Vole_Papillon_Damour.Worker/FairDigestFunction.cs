using MediatR;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vole_Papillon_Damour.Application.FairDigest.Commands.PlanFairDigest;
using Vole_Papillon_Damour.Application.FairDigest.Commands.SendDueFairDigests;
using Vole_Papillon_Damour.Application.FairDigest.Common;

namespace Vole_Papillon_Damour.Worker;

public sealed class FairDigestFunction(
    IServiceScopeFactory scopeFactory,
    ILogger<FairDigestFunction> logger,
    IOptions<FairDigestOptions> options)
{
    private readonly FairDigestOptions _options = options.Value;

    [Function("FairDigest")]
    public async Task Run(
        [TimerTrigger("%FairDigest:Schedule%")] TimerInfo timer,
        CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            logger.LogInformation("Fair digest timer skipped because FairDigest:Enabled is false.");
            return;
        }

        try
        {
            using var scope = scopeFactory.CreateScope();
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            var plan = await sender.Send(new PlanFairDigestCommand(), cancellationToken);
            if (plan.IsError)
            {
                throw new InvalidOperationException(
                    $"Fair digest planning failed: {string.Join("; ", plan.Errors.Select(error => error.Description))}");
            }

            var send = await sender.Send(new SendDueFairDigestsCommand(), cancellationToken);
            if (send.IsError)
            {
                throw new InvalidOperationException(
                    $"Fair digest delivery failed: {string.Join("; ", send.Errors.Select(error => error.Description))}");
            }

            logger.LogInformation(
                "Fair digest worker completed. PlanStatus: {PlanStatus}, PlannedAtUtc: {PlannedAtUtc}, " +
                "Campaigns: {Campaigns}, Sent: {Sent}, Skipped: {Skipped}, Failed: {Failed}",
                plan.Value.Status,
                plan.Value.PlannedAtUtc,
                send.Value.CampaignCount,
                send.Value.SentCount,
                send.Value.SkippedCount,
                send.Value.FailedCount);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Fair digest timer failed unexpectedly.");
            throw;
        }
    }
}
