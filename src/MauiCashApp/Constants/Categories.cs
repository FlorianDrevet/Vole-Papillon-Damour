using ShopAppVpd.Apis.Enums;
using ShopAppVpd.Dtos;

namespace ShopAppVpd.Constants;

public static class Categories
{
    public static readonly Category Buvette = new() { Name = "Buvette", Icon = "drink.png", Section = ProductSection.Bar };
    public static readonly Category Loto = new() { Name = "Loto", Icon = "loto.png", Section = ProductSection.Bingo };
    public static readonly Category Livres = new() { Name = "Livres", Icon = "book.png", Section = ProductSection.Book };
    
    public static readonly List<Category> All = new()
    {
        Buvette,
        Loto,
        Livres
    };
}