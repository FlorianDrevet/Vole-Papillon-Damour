using Vole_Papillon_Damour.Domain.ScanSessionAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Books.Common;

public sealed record BookAlertOperationResult(
    ScanSessionId ScanSessionId,
    int AffectedCount);
