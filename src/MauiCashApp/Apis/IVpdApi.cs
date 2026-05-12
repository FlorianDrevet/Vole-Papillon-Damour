using Refit;
using ShopAppVpd.Apis.Responses;
using ShopAppVpd.Services.Responses;

namespace ShopAppVpd.Apis;

[Headers("Accept: application/json")]
public interface IVpdApi
{
    [Get("/product")]
    Task<ApiResponse<List<ProductResponse>>> GetProductsAsync(CancellationToken cancellationToken);
}