namespace Vole_Papillon_Damour.Contracts.Books.Responses;

public sealed record ScanBookRemovalResponse(
    string Isbn13,
    int QtyAvailable,
    int QtyAnnounced,
    Guid ScanSessionId,
    bool AlreadyRemoved);
