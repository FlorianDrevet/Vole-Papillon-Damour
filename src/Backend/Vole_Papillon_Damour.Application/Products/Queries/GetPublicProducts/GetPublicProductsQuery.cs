using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.Products.Common;

namespace Vole_Papillon_Damour.Application.Products.Queries.GetPublicProducts;

public record GetPublicProductsQuery() : IRequest<ErrorOr<List<ProductResult>>>;
