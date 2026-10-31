using ShopAppVpd.Apis.Enums;
using ShopAppVpd.Apis.Responses;
using ShopAppVpd.Dtos;
using ShopAppVpd.Models;
using ShopAppVpd.Services;
using ShopAppVpd.Services.Responses;

namespace ShopAppVpd.Tests;

public sealed class CashierProductFilterTests
{
    [Fact]
    public void FromApi_OnlyIncludesAvailableBuvetteAndLotoProducts()
    {
        var products = CashierProductFilter.FromApi(
        [
            CreateResponse("Boisson disponible", ProductSection.Bar, isAvailable: true),
            CreateResponse("Carton loto", ProductSection.Bingo, isAvailable: true),
            CreateResponse("Produit désactivé", ProductSection.Bar, isAvailable: false),
            CreateResponse("Livre disponible", ProductSection.Book, isAvailable: true)
        ]);

        Assert.Equal(["Boisson disponible", "Carton loto"], products.Select(product => product.Name));
    }

    [Fact]
    public void FromCache_OnlyIncludesAvailableBuvetteAndLotoProducts()
    {
        var products = CashierProductFilter.FromCache(
        [
            CreateProduct("Boisson disponible", ProductSection.Bar, isAvailable: true),
            CreateProduct("Carton loto", ProductSection.Bingo, isAvailable: true),
            CreateProduct("Produit désactivé", ProductSection.Bar, isAvailable: false),
            CreateProduct("Livre disponible", ProductSection.Book, isAvailable: true)
        ]);

        Assert.Equal(["Boisson disponible", "Carton loto"], products.Select(product => product.Name));
    }

    [Fact]
    public void Categories_OnlyExposeBuvetteAndLoto()
    {
        Assert.Equal(
            [ProductSection.Bar, ProductSection.Bingo],
            ShopAppVpd.Constants.Categories.All.Select(category => category.Section));
    }

    private static ProductResponse CreateResponse(string name, ProductSection section, bool isAvailable)
        => new(
            Guid.NewGuid(),
            name,
            1.50,
            new Uri("https://example.test/product.png"),
            ProductCategory.Salt.ToString(),
            section.ToString(),
            [],
            0,
            isAvailable);

    private static Product CreateProduct(string name, ProductSection section, bool isAvailable)
        => new(
            Guid.NewGuid(),
            name,
            1.50,
            new Uri("https://example.test/product.png"),
            ProductCategory.Salt.ToString(),
            section.ToString(),
            [],
            0,
            isAvailable);
}
