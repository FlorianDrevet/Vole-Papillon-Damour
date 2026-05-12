using Vole_Papillon_Damour.Domain.Common.Models;

namespace Vole_Papillon_Damour.Domain.ProductAggregate.ValueObjects;

public class ProductCategory: EnumValueObject<ProductCategory.ProductCategoryEnum>
{
    public enum ProductCategoryEnum
    {
        Salt,
        Sugar,
        HotDrink,
        ColdDrink,
        Unknown
    }
    
    public ProductCategory(){}

    public ProductCategory(ProductCategoryEnum productCategoryEnum) : base(productCategoryEnum)
    {
    }

    public static ProductCategory? CreateFromString(string? status)
    {
        if (status is null)
        {
            return null;
        }
        return new ProductCategory(ParseOrDefault(status, ProductCategoryEnum.Unknown));
    }
}