using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.Products.Common;
using Vole_Papillon_Damour.Domain.ProductAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Products.Commands.AddPromotions;

public record AddPromotionCommand(
    ProductId ProductId,
    Promotion Promotion
): IRequest<ErrorOr<ProductResult>>;