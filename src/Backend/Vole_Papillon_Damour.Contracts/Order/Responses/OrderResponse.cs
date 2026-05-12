namespace Vole_Papillon_Damour.Contracts.Order.Responses;

public record OrderResponse(
    Guid OrderId,
    string FamilyName,
    string Status,
    double TotalPrice,
    List<ProductOrderedResponse> OrderedProduct);

public record ProductOrderedResponse(
    int Quantity,
    Guid ProductId
    );