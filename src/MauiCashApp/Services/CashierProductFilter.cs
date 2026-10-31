using ShopAppVpd.Apis.Responses;
using ShopAppVpd.Apis.Enums;
using ShopAppVpd.Dtos;

namespace ShopAppVpd.Services;

internal static class CashierProductFilter
{
    public static List<Product> FromApi(IEnumerable<ProductResponse> products)
        => products
            .Where(product => product.Available && IsCashierSection(product.ProductSection))
            .Select(product => new Product(product))
            .ToList();

    public static List<Product> FromCache(IEnumerable<Product> products)
        => products
            .Where(product => product.Available && IsCashierSection(product.ProductSection))
            .ToList();

    private static bool IsCashierSection(string section)
        => section is nameof(ProductSection.Bar) or nameof(ProductSection.Bingo);
}
