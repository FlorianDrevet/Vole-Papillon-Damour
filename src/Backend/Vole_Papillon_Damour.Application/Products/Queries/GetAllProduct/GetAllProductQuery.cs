using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.Products.Common;

namespace Vole_Papillon_Damour.Application.Products.Queries.GetAllProduct;

public record GetAllProductQuery(
    
) : IRequest<ErrorOr<List<ProductResult>>>;