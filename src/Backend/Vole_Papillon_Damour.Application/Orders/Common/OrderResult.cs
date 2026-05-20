using Vole_Papillon_Damour.Domain.OrderAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.ProductAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Orders.Common;

public record OrderResult(
    OrderId OrderId,
    string FamilyName,
    StatusEnum Status,
    double TotalPrice,
    List<ProductOrderedResult> OrderedProduct);

public record ProductOrderedResult(
    int Quantity,
    ProductId ProductId);