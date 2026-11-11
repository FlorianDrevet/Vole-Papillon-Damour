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

    [Fact]
    public void SelectingLivres_ShowsOnlyAvailableBooks()
    {
        var drink = CreateProduct(Guid.NewGuid(), "Boisson disponible", isAvailable: true);
        var book = CreateProduct(Guid.NewGuid(), "Livre disponible", isAvailable: true, ProductSection.Book, 2.00);
        var disabledBook = CreateProduct(Guid.NewGuid(), "Livre désactivé", isAvailable: false, ProductSection.Book);
        var viewModel = new MainViewModel(new StubProductService([drink, book, disabledBook]));

        viewModel.SetSelectedCategoryCommand.Execute(ShopAppVpd.Constants.Categories.Livres);

        Assert.Equal(["Livre disponible"], viewModel.FilteredProducts.Select(product => product.Name));
    }

    [Fact]
    public void AddingBook_TotalIncludesBookAndBuvetteProducts()
    {
        var drink = CreateProduct(Guid.NewGuid(), "Boisson", isAvailable: true, ProductSection.Bar, 3.00);
        var book = CreateProduct(Guid.NewGuid(), "Livre", isAvailable: true, ProductSection.Book, 2.00);
        var viewModel = new MainViewModel(new StubProductService([drink, book]));

        viewModel.AddToCartCommand.Execute(drink);
        viewModel.AddToCartCommand.Execute(book);

        Assert.Equal(5.00, viewModel.CartTotal, precision: 2);
    }

    [Fact]
    public void AddingSameBookTwice_IncreasesQuantityInsteadOfAddingLine()
    {
        var book = CreateProduct(Guid.NewGuid(), "Livre", isAvailable: true, ProductSection.Book, 2.00);
        var viewModel = new MainViewModel(new StubProductService([book]));

        viewModel.AddToCartCommand.Execute(book);
        viewModel.AddToCartCommand.Execute(book);

        var line = Assert.Single(viewModel.ShoppedProducts);
        Assert.Equal(2, line.Quantity);
        Assert.Equal(4.00, viewModel.CartTotal, precision: 2);
    }

    [Fact]
    public void LivresEmptyState_ShownOnlyWhenLivresSelectedAndNoAvailableBook()
    {
        var drink = CreateProduct(Guid.NewGuid(), "Boisson", isAvailable: true);
        var viewModel = new MainViewModel(new StubProductService([drink]));

        viewModel.SetSelectedCategoryCommand.Execute(ShopAppVpd.Constants.Categories.Buvette);
        Assert.False(viewModel.IsLivresEmptyVisible);

        viewModel.SetSelectedCategoryCommand.Execute(ShopAppVpd.Constants.Categories.Livres);
        Assert.True(viewModel.IsLivresEmptyVisible);
    }

    [Fact]
    public void LivresEmptyState_HiddenWhenABookIsAvailable()
    {
        var book = CreateProduct(Guid.NewGuid(), "Livre", isAvailable: true, ProductSection.Book, 2.00);
        var viewModel = new MainViewModel(new StubProductService([book]));

        viewModel.SetSelectedCategoryCommand.Execute(ShopAppVpd.Constants.Categories.Livres);

        Assert.False(viewModel.IsLivresEmptyVisible);
    }

    [Fact]
    public async Task IsLoading_TrueWhileCatalogueLoads_ThenFalse()
    {
        var service = new PendingProductService();
        var viewModel = new MainViewModel(service);

        Assert.True(viewModel.IsLoading);

        service.Complete(new ProductCatalog([], IsFromCache: false));
        await viewModel.LoadProductsCommand.ExecutionTask!;

        Assert.False(viewModel.IsLoading);
    }

    [Fact]
    public async Task CatalogueLoadedWhileLivresSelected_ShowsBooks()
    {
        var service = new PendingProductService();
        var viewModel = new MainViewModel(service);
        viewModel.SetSelectedCategoryCommand.Execute(ShopAppVpd.Constants.Categories.Livres);

        var book = CreateProduct(Guid.NewGuid(), "Livre", isAvailable: true, ProductSection.Book, 2.00);
        service.Complete(new ProductCatalog([book], IsFromCache: false));
        await viewModel.LoadProductsCommand.ExecutionTask!;

        Assert.Equal(["Livre"], viewModel.FilteredProducts.Select(product => product.Name));
    }

    [Fact]
    public void CatalogueFromCache_SetsOfflineFlag_AndKeepsCachedBooks()
    {
        var book = CreateProduct(Guid.NewGuid(), "Livre", isAvailable: true, ProductSection.Book, 2.00);
        var viewModel = new MainViewModel(new StubProductService([book], isFromCache: true));

        viewModel.SetSelectedCategoryCommand.Execute(ShopAppVpd.Constants.Categories.Livres);

        Assert.True(viewModel.IsCatalogFromCache);
        Assert.Single(viewModel.FilteredProducts);
    }

    private static Product CreateProduct(
        Guid id,
        string name,
        bool isAvailable,
        ProductSection section = ProductSection.Bar,
        double price = 1.50)
    {
        return new Product(new ProductResponse(
            id,
            name,
            price,
            new Uri("https://example.test/product.png"),
            ProductCategory.Salt.ToString(),
            section.ToString(),
            [],
            0,
            isAvailable));
    }

    private sealed class StubProductService(IEnumerable<Product> products, bool isFromCache = false) : IProductService
    {
        public Task<ProductCatalog> GetProductsAsync(CancellationToken cancellationToken)
            => Task.FromResult(new ProductCatalog(products, isFromCache));
    }

    private sealed class PendingProductService : IProductService
    {
        private readonly TaskCompletionSource<ProductCatalog> _completion = new();

        public Task<ProductCatalog> GetProductsAsync(CancellationToken cancellationToken) => _completion.Task;

        public void Complete(ProductCatalog catalog) => _completion.SetResult(catalog);
    }
}
