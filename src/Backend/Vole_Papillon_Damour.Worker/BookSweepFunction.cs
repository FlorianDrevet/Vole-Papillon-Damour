using MediatR;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Vole_Papillon_Damour.Application.Books.Commands.Background;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.Common.Services;

namespace Vole_Papillon_Damour.Worker;

public sealed class BookSweepFunction(
    IServiceScopeFactory scopeFactory,
    ILogger<BookSweepFunction> logger)
{
    [Function("Sweep")]
    public async Task Run(
        [TimerTrigger("0 */5 * * * *")] TimerInfo timer,
        CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var closeResult = await sender.Send(
            new CloseIdleScanSessionsCommand(),
            cancellationToken);
        var attachResult = await sender.Send(
            new AttachUndatedAnnouncementsToNextFairCommand(),
            cancellationToken);
        var releaseResult = await sender.Send(
            new ReleaseDueAnnouncementsCommand(),
            cancellationToken);
        var alertDelivery = scope.ServiceProvider
            .GetRequiredService<BookAlertDeliveryService>();
        var alertResult = await alertDelivery.ProcessPendingAsync(cancellationToken);

        var accountDeletionService = scope.ServiceProvider.GetRequiredService<IAccountDeletionService>();
        var deletedAccounts = await accountDeletionService.ProcessPendingAsync(cancellationToken);

        logger.LogInformation(
            "Worker sweep completed. IdleCandidates: {IdleCandidates}, IdleClosed: {IdleClosed}, " +
            "UndatedAttached: {UndatedAttached}, DetachedFromCancelledFairs: {DetachedFromCancelledFairs}, " +
            "ReleasedAnnouncements: {ReleasedAnnouncements}, " +
            "ReleasedQuantity: {ReleasedQuantity}, AlertClaimed: {AlertClaimed}, " +
            "AlertSent: {AlertSent}, AlertCancelled: {AlertCancelled}, AlertFailed: {AlertFailed}, " +
            "AlertDisabled: {AlertDisabled}, DueUnreleasedAnnouncements: {DueUnreleasedAnnouncements}, " +
            "DeletedAccounts: {DeletedAccounts}",
            closeResult.CandidateCount,
            closeResult.ClosedCount,
            attachResult.AttachedCount,
            attachResult.DetachedCount,
            releaseResult.ReleasedCount,
            releaseResult.ReleasedQuantity,
            alertResult.ClaimedCount,
            alertResult.SentCount,
            alertResult.CancelledCount,
            alertResult.FailedCount,
            alertResult.Disabled,
            releaseResult.DueUnreleasedCount,
            deletedAccounts);

        if (releaseResult.DueUnreleasedCount > 0)
        {
            logger.LogWarning(
                "Book announcements are late and could not be released. Count: {DueUnreleasedCount}",
                releaseResult.DueUnreleasedCount);
        }
    }
}
