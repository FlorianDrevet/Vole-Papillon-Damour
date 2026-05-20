using ErrorOr;
using MapsterMapper;
using MediatR;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Products.Common;

namespace Vole_Papillon_Damour.Application.Products.Queries.GetAllProduct;

public class GetAllProductQueryHandler(IProductRepository productRepository, IMapper mapper)
    : IRequestHandler<GetAllProductQuery, ErrorOr<List<ProductResult>>>
{
    public async Task<ErrorOr<List<ProductResult>>> Handle(GetAllProductQuery command, CancellationToken cancellationToken)
    {
        var res = await productRepository.GetAllAsync();
        return mapper.Map<List<ProductResult>>(res);
    }
}