using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Domain.ProductAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Products.Commands.ReorderBookProducts;

public sealed record ReorderBookProductsCommand(IReadOnlyList<ProductId> ProductIds) : IRequest<ErrorOr<bool>>;
