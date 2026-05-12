using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.Orders.Common;
using Vole_Papillon_Damour.Domain.OrderAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.ProductAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Orders.Commands.CreateOrder;

public record CreateOrderCommand(
    string FamilyName,
    StatusEnum Status,
    double TotalPrice,
    List<OrderedProductCommand> OrderedProduct
) : IRequest<ErrorOr<OrderResult>>;

public record OrderedProductCommand(
    int Quantity,
    ProductId ProductId);