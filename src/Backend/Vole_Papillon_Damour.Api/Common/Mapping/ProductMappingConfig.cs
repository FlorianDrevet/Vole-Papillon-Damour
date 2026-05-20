using Azure.Core;
using Mapster;
using Vole_Papillon_Damour.Application.Products.Commands.AddPromotions;
using Vole_Papillon_Damour.Application.Products.Commands.CreateProduct;
using Vole_Papillon_Damour.Application.Products.Commands.DeleteProduct;
using Vole_Papillon_Damour.Application.Products.Commands.UpdateProduct;
using Vole_Papillon_Damour.Application.Products.Common;
using Vole_Papillon_Damour.Contracts.Product.Requests;
using Vole_Papillon_Damour.Contracts.Product.Responses;
using Vole_Papillon_Damour.Domain.ProductAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Api.Common.Mapping;

public class ProductMappingConfig : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        TypeAdapterConfig<IFormFile, Stream>.ForType().MapWith(src => src.OpenReadStream());
        
        TypeAdapterConfig<ProductId, Guid>.ForType().MapWith(src => src.Value);
        
        TypeAdapterConfig<Guid, ProductId>.ForType().MapWith(src => new ProductId(src));
        
        TypeAdapterConfig<ProductSection, string>.ForType().MapWith(src => src.Value.ToString());
        TypeAdapterConfig<ProductCategory?, string?>.ForType().MapWith(src => src == null ? null : src.Value.ToString());
        
        TypeAdapterConfig<string, ProductSection>.ForType().MapWith(src => ProductSection.CreateFromString(src));
        TypeAdapterConfig<string?, ProductCategory?>.ForType().MapWith(src => ProductCategory.CreateFromString(src));

        config.NewConfig<CreateProductRequest, CreateProductCommand>()
            .Map(dest => dest.ImageName, src => src.Image!.FileName)
            .Map(dest => dest, src => src);

        config.NewConfig<AddPromotionRequest, AddPromotionCommand>()
            .Map(dest => dest.Promotion, src => new Promotion(src.Quantity, src.DiscountedPrice));
        
        config.NewConfig<(UpdateProductRequest Request, Guid Id), UpdateProductCommand>()
            .Map(dest => dest.ProductId, src => src.Id)
            .Map(dest => dest, src => src.Request);
    }
}