namespace Vole_Papillon_Damour.Contracts.Books.Requests;

public sealed record RegisterSaleRequest(
    string Isbn,
    int Quantity,
    DateTime OccurredAt,
    Guid ClientGestureId);
