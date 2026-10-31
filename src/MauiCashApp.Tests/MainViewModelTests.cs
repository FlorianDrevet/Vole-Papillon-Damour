using ShopAppVpd.Apis.Enums;
using ShopAppVpd.Apis.Responses;
using ShopAppVpd.Dtos;
using ShopAppVpd.Interfaces;
using ShopAppVpd.Services.Responses;
using ShopAppVpd.ViewModels;

namespace ShopAppVpd.Tests;

public sealed class MainViewModelTests
{
    [Fact]
    public void ApplyFilter_DisabledProduct_ExcludedFromCashierList()
    {
        var availableProduct = CreateProduct(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            "Boisson disponible",
            isAvailable: true);
        var disabledProduct = CreateProduct(
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            "Produit désactivé",
            isAvailable: false);
        var viewModel = new MainViewModel(new StubProductService([availableProduct, disabledProduct]));

        Assert.Collection(viewModel.FilteredProducts, product => Assert.Equal(availableProduct.Id, product.Id));
    }

    private static Product CreateProduct(Guid id, string name, bool isAvailable)
    {
        return new Product(new ProductResponse(
            id,
            name,
            1.50,
            new Uri("https://example.test/product.png"),
            ProductCategory.Salt.ToString(),
            ProductSection.Bar.ToString(),
            [],
            0,
            isAvailable));
    }

    private sealed class StubProductService(IEnumerable<Product> products) : IProductService
    {
        public Task<IEnumerable<Product>> GetProductsAsync(CancellationToken cancellationToken)
            => Task.FromResult(products);
    }
}
