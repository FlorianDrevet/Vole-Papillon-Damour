using ShopAppVpd.Dtos;

namespace ShopAppVpd.Interfaces;

public interface IProductService
{
    Task<IEnumerable<Product>> GetProductsAsync(CancellationToken cancellationToken);
}
