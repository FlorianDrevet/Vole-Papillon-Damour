namespace Vole_Papillon_Damour.Contracts.Books.Responses;

public sealed record RegisterSaleResponse(
    string Isbn13,
    Guid SaleMovementId,
    int Quantity,
    int QtyAvailable,
    int SalesCount,
    Guid? AssoEventsId,
    string FairMatchStatus,
    bool HadNoAvailableStock,
    bool HadUnreleasedAnnouncement,
    bool IsRare,
    bool ClockSuspect,
    bool AlreadyProcessed);
