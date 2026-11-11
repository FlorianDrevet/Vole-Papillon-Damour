using ShopAppVpd.Apis;
using ShopAppVpd.Databases;
using ShopAppVpd.Dtos;
using ShopAppVpd.Interfaces;

namespace ShopAppVpd.Services;

internal sealed class ProductService: IProductService
{
    private readonly ProductDatabase _productDatabase;
    private readonly IVpdApi _client;

    public ProductService(IVpdApi client, ProductDatabase productDatabase)
    {
        _client = client;
        _productDatabase = productDatabase;
    }

    public async Task<ProductCatalog> GetProductsAsync(CancellationToken cancellationToken)
    {
        if (Connectivity.Current.NetworkAccess == NetworkAccess.Internet)
        {
            try
            {
                var productsResponse = await _client.GetProductsAsync(cancellationToken);

                if (productsResponse.IsSuccessful)
                {
                    var cashierProducts = CashierProductFilter.FromApi(productsResponse.Content);
                    await _productDatabase.SaveProductsAsync(cashierProducts);
                    return new ProductCatalog(cashierProducts, IsFromCache: false);
                }
                else
                {
                    return await GetAvailableCachedProductsAsync();
                }
            }
            catch
            {
                return await GetAvailableCachedProductsAsync();
            }
        }
        else
        {
            return await GetAvailableCachedProductsAsync();
        }
    }

    private async Task<ProductCatalog> GetAvailableCachedProductsAsync()
    {
        var cachedProducts = await _productDatabase.GetProductsAsync();
        return new ProductCatalog(CashierProductFilter.FromCache(cachedProducts), IsFromCache: true);
    }
}
