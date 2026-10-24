using Vole_Papillon_Damour.Domain.IsbndbAggregate;

namespace Vole_Papillon_Damour.Application.Common.Interfaces.Services;

public interface IIsbndbQuotaGuard
{
    Task<bool> IsExhaustedAsync(DateTime now, CancellationToken cancellationToken);

    Task ObserveAsync(
        int dailyLimit,
        int dailyRemaining,
        DateTime resetsAt,
        DateTime observedAt,
        CancellationToken cancellationToken);

    Task MarkExhaustedAsync(DateTime resetsAt, DateTime at, CancellationToken cancellationToken);

    Task RecordFailureAsync(string reason, DateTime at, CancellationToken cancellationToken);

    Task RecordUsageAsync(IsbndbUsage usage, int credits, CancellationToken cancellationToken);
}
