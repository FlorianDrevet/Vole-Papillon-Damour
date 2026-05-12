using ShopAppVpd.Apis.Enums;

namespace ShopAppVpd.Models;

public class FoodSection
{
    public required string Name { get; set; }
    public required string Icon { get; set; } // chemin image
    
    public required ProductCategory Section { get; set; }
}