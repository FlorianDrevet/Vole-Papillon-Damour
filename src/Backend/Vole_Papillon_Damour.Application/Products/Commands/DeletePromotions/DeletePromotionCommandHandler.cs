using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Products.Common;
using Vole_Papillon_Damour.Domain.Common.Errors;
using Vole_Papillon_Damour.Domain.ProductAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Products.Commands.DeletePromotions;

public class DeletePromotionCommandHandler(
    IProductRepository productRepository)
    : IRequestHandler<DeletePromotionCommand, ErrorOr<bool>>
{
    public async Task<ErrorOr<bool>> Handle(DeletePromotionCommand command, CancellationToken cancellationToken)
    {
        var product = await productRepository.GetByIdAsync(command.ProductId);

        if (product is null)
            return Errors.Product.ProductNotFound();

        var isDeletionSuccess = product.DeletePromotion(new Promotion(command.Quantity, command.DiscountedPrice));

        if (!isDeletionSuccess)
        {
            return Errors.Promotion.PromotionNotFound();
        }

        await productRepository.UpdateAsync(product);
        return true;
    } 
}