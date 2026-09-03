using Vole_Papillon_Damour.Domain.ScanSessionAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;

public interface IBookAlertOutbox
{
    Task QueueForSessionAsync(
        ScanSessionId scanSessionId,
        DateTime closedAt,
        CancellationToken cancellationToken);
}
