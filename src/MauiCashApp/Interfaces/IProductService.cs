using ShopAppVpd.Dtos;

namespace ShopAppVpd.Interfaces;

public interface IProductService
{
    Task<ProductCatalog> GetProductsAsync(CancellationToken cancellationToken);
}
