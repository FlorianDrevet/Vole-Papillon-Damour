using Vole_Papillon_Damour.Domain.Common.Models;

namespace Vole_Papillon_Damour.Domain.ProductAggregate.ValueObjects;

public class ProductSection: EnumValueObject<ProductSection.ProductSectionEnum>
{
    public enum ProductSectionEnum
    {
        Book,
        Bar,
        Bingo,
        Unknown
    }
    
    public ProductSection(){}

    public ProductSection(ProductSectionEnum productSectionEnum): base(productSectionEnum)
    {
    }

    public static ProductSection CreateFromString(string? status)
    {
        return new ProductSection(ParseOrDefault(status, ProductSectionEnum.Unknown));
    }
}