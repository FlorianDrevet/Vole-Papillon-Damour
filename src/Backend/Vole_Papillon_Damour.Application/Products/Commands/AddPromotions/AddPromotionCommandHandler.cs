using ErrorOr;
using MapsterMapper;
using MediatR;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Products.Common;
using Vole_Papillon_Damour.Domain.Common.Errors;

namespace Vole_Papillon_Damour.Application.Products.Commands.AddPromotions;

public class AddPromotionCommandHandler(
    IMapper mapper,
    IProductRepository productRepository)
    : IRequestHandler<AddPromotionCommand, ErrorOr<ProductResult>>
{
    public async Task<ErrorOr<ProductResult>> Handle(AddPromotionCommand command, CancellationToken cancellationToken)
    {
        var product = await productRepository.GetByIdAsync(command.ProductId);
        if (product is null)
        {
            return Errors.Product.ProductNotFound();
        }
        
        var isAddSuccess = product.AddPromotion(command.Promotion);
        if (!isAddSuccess)
        {
            return Errors.Promotion.PromotionAlreadyExists();
        }

        await productRepository.UpdateAsync(product);
        
        return mapper.Map<ProductResult>(product);
    } 
}