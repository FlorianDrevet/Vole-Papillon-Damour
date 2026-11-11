using Vole_Papillon_Damour.Domain.ScanSessionAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Books.Common;

public sealed record ScanBookRemovalResult(
    string Isbn13,
    int QuantityAvailable,
    int QuantityAnnounced,
    ScanSessionId ScanSessionId,
    bool AlreadyRemoved);
