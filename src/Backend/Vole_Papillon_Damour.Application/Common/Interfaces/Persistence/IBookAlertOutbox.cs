using Vole_Papillon_Damour.Domain.ScanSessionAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.BookAggregate.ValueObjects;
using Vole_Papillon_Damour.Application.Common.Models;

namespace Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;

public interface IBookAlertOutbox
{
    Task QueueForSessionAsync(
        ScanSessionId scanSessionId,
        DateTime closedAt,
        CancellationToken cancellationToken);

    Task<int> CancelPendingForSessionAsync(
        ScanSessionId scanSessionId,
        CancellationToken cancellationToken);

    Task<int> ForcePendingForSessionAsync(
        ScanSessionId scanSessionId,
        DateTime dueAt,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<BookAlertOutboxWorkItem>> ClaimDueAsync(
        DateTime now,
        TimeSpan lease,
        int batchSize,
        CancellationToken cancellationToken);

    Task<DateTime?> GetOldestDueAtAsync(
        DateTime now,
        CancellationToken cancellationToken);

    Task<BookAlertDelivery?> GetPendingDeliveryAsync(
        Guid messageId,
        DateTime claimedUntil,
        DateTime now,
        CancellationToken cancellationToken);

    Task<int> CancelAsync(
        Guid messageId,
        DateTime claimedUntil,
        CancellationToken cancellationToken);

    Task<bool> MarkSentAsync(
        Guid messageId,
        DateTime claimedUntil,
        DateTime sentAt,
        IReadOnlyCollection<Isbn13> itemIsbn13s,
        CancellationToken cancellationToken);

    Task RecordFailureAsync(
        Guid messageId,
        DateTime claimedUntil,
        string failureCode,
        DateTime failedAt,
        CancellationToken cancellationToken);
}
