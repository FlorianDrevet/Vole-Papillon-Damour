namespace Vole_Papillon_Damour.Contracts.Books.Requests;

/// <summary>Describes a cashier's request to void a previously registered sale.</summary>
/// <param name="OccurredAt">The UTC time when the cashier requested the correction.</param>
/// <param name="ClientGestureId">The stable idempotency key for this correction.</param>
public sealed record VoidSaleRequest(
    DateTime OccurredAt,
    Guid ClientGestureId);
