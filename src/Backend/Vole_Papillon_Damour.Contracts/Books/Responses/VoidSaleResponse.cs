namespace Vole_Papillon_Damour.Contracts.Books.Responses;

/// <summary>Confirms the reversal of an ordinary scan cashier sale.</summary>
/// <param name="Isbn13">The ISBN of the sold book.</param>
/// <param name="OriginalSaleMovementId">The movement id of the sale being reversed.</param>
/// <param name="ReversalMovementId">The movement id that restores the book quantity.</param>
/// <param name="Quantity">The quantity restored by the reversal.</param>
/// <param name="QtyAvailable">The current availability after the reversal.</param>
/// <param name="SalesCount">The current sales count after the reversal.</param>
/// <param name="AssoEventsId">The fair associated with the original sale, when present.</param>
/// <param name="ClockSuspect">Whether the supplied client timestamp looked unreliable.</param>
/// <param name="AlreadyProcessed">Whether this idempotency key had already been applied.</param>
public sealed record VoidSaleResponse(
    string Isbn13,
    Guid OriginalSaleMovementId,
    Guid ReversalMovementId,
    int Quantity,
    int QtyAvailable,
    int SalesCount,
    Guid? AssoEventsId,
    bool ClockSuspect,
    bool AlreadyProcessed);
