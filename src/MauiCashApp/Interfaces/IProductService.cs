using ShopAppVpd.Dtos;
using ErrorOr;

namespace ShopAppVpd.Interfaces;

public interface IProductService
{
    Task<IEnumerable<Product>> GetProductsAsync(CancellationToken cancellationToken);
}