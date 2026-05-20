using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Domain.ProductAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Products.Commands.DeleteProduct;

public record DeleteProductCommand(
    ProductId ProductId
) : IRequest<ErrorOr<bool>>;