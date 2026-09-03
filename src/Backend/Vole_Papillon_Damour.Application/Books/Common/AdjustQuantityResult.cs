using Vole_Papillon_Damour.Domain.BookMovementAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Books.Common;

public sealed record AdjustQuantityResult(
    string Isbn13,
    int PreviousQuantityAvailable,
    int QuantityAvailable,
    int Delta,
    BookMovementId? MovementId,
    bool Changed);
