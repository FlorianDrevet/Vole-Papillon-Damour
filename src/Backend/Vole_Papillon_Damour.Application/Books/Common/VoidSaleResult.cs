using Vole_Papillon_Damour.Domain.BookMovementAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Books.Common;

public sealed record VoidSaleResult(
    string Isbn13,
    BookMovementId OriginalSaleMovementId,
    BookMovementId ReversalMovementId,
    int Quantity,
    int QuantityAvailable,
    int SalesCount,
    AssoEventsId? AssoEventsId,
    bool ClockSuspect,
    bool AlreadyProcessed);
