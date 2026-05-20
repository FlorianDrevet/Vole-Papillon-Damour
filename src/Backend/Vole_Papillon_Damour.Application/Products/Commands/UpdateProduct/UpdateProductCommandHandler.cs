using ErrorOr;
using MapsterMapper;
using MediatR;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.Events.Common;
using Vole_Papillon_Damour.Application.Products.Common;
using Vole_Papillon_Damour.Domain.Common.Errors;

namespace Vole_Papillon_Damour.Application.Products.Commands.UpdateProduct;

public class UpdateProductCommandHandler(IProductRepository productRepository, IBlobService blobService, IMapper mapper)
    : IRequestHandler<UpdateProductCommand, ErrorOr<ProductResult>>
{
    public async Task<ErrorOr<ProductResult>> Handle(UpdateProductCommand command, CancellationToken cancellationToken)
    {
        var product = await productRepository.GetByIdAsync(command.ProductId);

        if (product is null)
        {
            return Errors.Product.ProductNotFound();
        }

        var urlImagePrincipal = command.UrlImage;
        if (urlImagePrincipal is null)
        {
            urlImagePrincipal = await blobService.UploadProductsImagesAsync(command.Image!.FileName,
                command.Image!.OpenReadStream());
        }
        
        product.Update(
            command.Name,
            command.Price,
            urlImagePrincipal,
            command.Available,
            command.ProductSection,
            command.ProductCategory
        );

        product = await productRepository.UpdateAsync(product);
        return mapper.Map<ProductResult>(product);
    }
}