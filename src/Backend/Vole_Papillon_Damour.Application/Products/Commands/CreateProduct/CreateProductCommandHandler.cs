using ErrorOr;
using MapsterMapper;
using MediatR;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.Products.Common;

namespace Vole_Papillon_Damour.Application.Products.Commands.CreateProduct;

public class CreateProductCommandHandler(
    IProductRepository productRepository,
    IBlobService blobService,
    IMapper mapper)
    : IRequestHandler<CreateProductCommand, ErrorOr<ProductResult>>
{
    public async Task<ErrorOr<ProductResult>> Handle(CreateProductCommand command, CancellationToken cancellationToken)
    {
        var urlImage = await blobService.UploadProductsImagesAsync(command.ImageName, command.Image);
                    
        var allProducts = await productRepository.GetAllAsync();
        var product = Domain.ProductAggregate.Product.Create(command.Name, command.Price, urlImage,
            command.ProductSection, command.Available, allProducts, command.ProductCategory,
            command.VisibleOnWebsite);
        
        product = await productRepository.AddAsync(product);
        
        return mapper.Map<ProductResult>(product);
    } 
}
