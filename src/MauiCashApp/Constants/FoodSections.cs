using ShopAppVpd.Apis.Enums;
using ShopAppVpd.Models;

namespace ShopAppVpd.Constants;

public static class FoodSections
{
    public static readonly FoodSection Salt = new() { Name = "Salé", Icon = "salt.png", Section = ProductCategory.Salt };
    public static readonly FoodSection Sugar = new() { Name = "Sucré", Icon = "sugar.png", Section = ProductCategory.Sugar };
    public static readonly FoodSection ColdDrink = new() { Name = "Froid", Icon = "cold_drink.png", Section = ProductCategory.ColdDrink };
    public static readonly FoodSection HotDrink = new() { Name = "Chaude", Icon = "hot_drink.png", Section = ProductCategory.HotDrink };
    
    public static readonly List<FoodSection> All = new()
    {
        Salt,
        Sugar,
        ColdDrink,
        HotDrink
    };
}