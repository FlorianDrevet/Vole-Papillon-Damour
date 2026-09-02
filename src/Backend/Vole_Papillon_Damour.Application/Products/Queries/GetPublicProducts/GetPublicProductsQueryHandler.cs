using ErrorOr;
using MapsterMapper;
using MediatR;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Products.Common;

namespace Vole_Papillon_Damour.Application.Products.Queries.GetPublicProducts;

public class GetPublicProductsQueryHandler(IProductRepository productRepository, IMapper mapper)
    : IRequestHandler<GetPublicProductsQuery, ErrorOr<List<ProductResult>>>
{
    public async Task<ErrorOr<List<ProductResult>>> Handle(
        GetPublicProductsQuery query,
        CancellationToken cancellationToken)
    {
        var products = await productRepository.GetAllAsync();
        var publicProducts = products
            .Where(product => product.Available && product.VisibleOnWebsite)
            .ToList();

        return mapper.Map<List<ProductResult>>(publicProducts);
    }
}
