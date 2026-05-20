using ShopAppVpd.Apis.Enums;

namespace ShopAppVpd.Dtos;

public class Category
{
    public required string Name { get; set; }
    public required string Icon { get; set; } // chemin image
    
    public required ProductSection Section { get; set; }
}