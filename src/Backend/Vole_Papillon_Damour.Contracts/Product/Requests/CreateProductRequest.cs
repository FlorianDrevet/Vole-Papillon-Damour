using Microsoft.AspNetCore.Http;

namespace Vole_Papillon_Damour.Contracts.Product.Requests;

public class CreateProductRequest
{
    public string? Name { get; set; }
    public double? Price { get; set; }
    public IFormFile? Image { get; set; }
    public string ProductCategory { get; set; } = string.Empty;
    public string? ProductSection { get; set; }
    public bool? Available { get; set; }
    public bool? VisibleOnWebsite { get; set; }
}
