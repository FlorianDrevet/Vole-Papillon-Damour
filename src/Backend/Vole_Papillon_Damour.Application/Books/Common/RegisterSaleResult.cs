using Vole_Papillon_Damour.Domain.BookMovementAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Books.Common;

public enum SaleFairMatchStatus : byte
{
    Attached,
    NoOpenFair,
    OverlappingOpenFairs
}

public sealed record RegisterSaleResult(
    string Isbn13,
    BookMovementId SaleMovementId,
    int Quantity,
    int QuantityAvailable,
    int SalesCount,
    AssoEventsId? AssoEventsId,
    SaleFairMatchStatus FairMatchStatus,
    bool HadNoAvailableStock,
    bool HadUnreleasedAnnouncement,
    bool IsRare,
    bool ClockSuspect,
    bool AlreadyProcessed);
