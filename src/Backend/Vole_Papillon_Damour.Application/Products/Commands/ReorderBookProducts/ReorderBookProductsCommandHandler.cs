using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Domain.Common.Errors;
using Vole_Papillon_Damour.Domain.ProductAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Products.Commands.ReorderBookProducts;

public sealed class ReorderBookProductsCommandHandler(IProductRepository productRepository)
    : IRequestHandler<ReorderBookProductsCommand, ErrorOr<bool>>
{
    public async Task<ErrorOr<bool>> Handle(
        ReorderBookProductsCommand command,
        CancellationToken cancellationToken)
    {
        var allProducts = await productRepository.GetAllAsync();
        var bookProducts = allProducts
            .Where(product => product.ProductSection.Value == ProductSection.ProductSectionEnum.Book)
            .ToList();
        var requestedIds = command.ProductIds.Select(productId => productId.Value).ToHashSet();
        var bookProductIds = bookProducts.Select(product => product.Id.Value).ToHashSet();

        if (command.ProductIds.Count != bookProducts.Count || !requestedIds.SetEquals(bookProductIds))
        {
            return Errors.Product.InvalidBookProductOrder();
        }

        var productsById = bookProducts.ToDictionary(product => product.Id.Value);
        for (var index = 0; index < command.ProductIds.Count; index++)
        {
            productsById[command.ProductIds[index].Value].Index = index + 1;
        }

        await productRepository.UpdateIndicesAsync(bookProducts, cancellationToken);
        return true;
    }
}
