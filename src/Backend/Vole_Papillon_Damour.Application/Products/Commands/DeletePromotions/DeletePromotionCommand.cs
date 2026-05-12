using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.Products.Common;
using Vole_Papillon_Damour.Domain.ProductAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Products.Commands.DeletePromotions;

public record DeletePromotionCommand(
    ProductId ProductId,
    int Quantity,
    double DiscountedPrice
): IRequest<ErrorOr<bool>>;