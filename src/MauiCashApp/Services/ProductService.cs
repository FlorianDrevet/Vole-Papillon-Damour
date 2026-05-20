using ShopAppVpd.Apis;
using ShopAppVpd.Databases;
using ShopAppVpd.Dtos;
using ShopAppVpd.Interfaces;
using ErrorOr;

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

    public async Task<IEnumerable<Product>> GetProductsAsync(CancellationToken cancellationToken)
    {
        if (Connectivity.Current.NetworkAccess == NetworkAccess.Internet)
        {
            try
            {
                var productsResponse = await _client.GetProductsAsync(cancellationToken);

                if (productsResponse.IsSuccessful)
                {
                    var products = productsResponse.Content.ConvertAll(p => new Product(p));
                    await _productDatabase.SaveProductsAsync(products);
                    return products;
                }
                else
                {
                    return await _productDatabase.GetProductsAsync();
                }
            }
            catch
            {
                return await _productDatabase.GetProductsAsync();
            }
        }
        else
        {
            return await _productDatabase.GetProductsAsync();
        }
    }
}