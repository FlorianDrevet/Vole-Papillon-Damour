using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.Common.Models;
using Microsoft.Extensions.Logging;

namespace Vole_Papillon_Damour.Application.Common.Services;

public sealed class BookAlertDeliveryService(
    IBookAlertOutbox outbox,
    IBookAlertEmailSender emailSender,
    IDateTimeProvider dateTimeProvider,
    ILogger<BookAlertDeliveryService>? logger = null)
{
    private const int ClaimBatchSize = 50;
    private static readonly TimeSpan ClaimLease = TimeSpan.FromMinutes(5);

    public async Task<BookAlertDeliveryResult> ProcessPendingAsync(
        CancellationToken cancellationToken)
    {
        var now = dateTimeProvider.UtcNow;
        EnsureUtc(now);
        var oldestDueAt = await outbox.GetOldestDueAtAsync(now, cancellationToken);
        if (oldestDueAt is not null)
        {
            var ageMinutes = Math.Max(0, (now - oldestDueAt.Value).TotalMinutes);
            logger?.LogInformation(
                "Book alert queue snapshot. OldestDueAgeMinutes: {OldestDueAgeMinutes}",
                ageMinutes);
            if (ageMinutes >= 30)
            {
                logger?.LogWarning(
                    "Book alert queue is late. OldestDueAt: {OldestDueAt}",
                    oldestDueAt.Value);
            }
        }

        if (!emailSender.IsEnabled)
        {
            logger?.LogInformation("Book alert delivery is disabled; no messages were claimed.");
            return new BookAlertDeliveryResult(0, 0, 0, 0, Disabled: true);
        }

        var workItems = await outbox.ClaimDueAsync(
            now,
            ClaimLease,
            ClaimBatchSize,
            cancellationToken);

        var sentCount = 0;
        var cancelledCount = 0;
        var failedCount = 0;
        foreach (var workItem in workItems)
        {
            try
            {
                var deliveryNow = dateTimeProvider.UtcNow;
                EnsureUtc(deliveryNow);
                var delivery = await outbox.GetPendingDeliveryAsync(
                    workItem.MessageId,
                    workItem.ClaimedUntil,
                    deliveryNow,
                    cancellationToken);
                if (delivery is null || delivery.Items.Count == 0)
                {
                    cancelledCount += await outbox.CancelAsync(
                        workItem.MessageId,
                        workItem.ClaimedUntil,
                        cancellationToken);
                    continue;
                }

                await emailSender.SendAsync(delivery, cancellationToken);
                var sentAt = dateTimeProvider.UtcNow;
                EnsureUtc(sentAt);
                if (!await outbox.MarkSentAsync(
                        workItem.MessageId,
                        workItem.ClaimedUntil,
                        sentAt,
                        delivery.Items.Select(item => item.Isbn13).ToArray(),
                        cancellationToken))
                {
                    logger?.LogWarning(
                        "Book alert email was accepted but its outbox lease was lost before acknowledgement. " +
                        "MessageId: {MessageId}",
                        workItem.MessageId);
                }
                else
                {
                    sentCount++;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                failedCount++;
                var failedAt = dateTimeProvider.UtcNow;
                EnsureUtc(failedAt);
                await outbox.RecordFailureAsync(
                    workItem.MessageId,
                    workItem.ClaimedUntil,
                    CreateFailureCode(exception),
                    failedAt,
                    cancellationToken);
            }
        }

        return new BookAlertDeliveryResult(
            workItems.Count,
            sentCount,
            cancelledCount,
            failedCount,
            Disabled: false);
    }

    private static string CreateFailureCode(Exception exception)
    {
        var code = exception.GetType().Name;
        return code.Length <= 128 ? code : code[..128];
    }

    private static void EnsureUtc(DateTime value)
    {
        if (value.Kind != DateTimeKind.Utc)
        {
            throw new InvalidOperationException("The worker clock must be expressed in UTC.");
        }
    }
}
