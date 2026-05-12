using ShopAppVpd.Apis.Responses;

namespace ShopAppVpd.Dtos;


using SQLite;
using System;
using System.Collections.Generic;

public class Product
{
    [PrimaryKey]
    public Guid Id { get; set; }

    public string Name { get; set; }

    public double Price { get; set; }
    
    public string UrlImage { get; set; }

    public string? ProductCategory { get; set; }

    public string ProductSection { get; set; }
    
    public string PromotionsJson { get; set; }

    public int Index { get; set; }

    public bool Available { get; set; }
    
    public Product() { }

    public Product(Guid id, string name, double price, Uri urlImage, string? productCategory,
        string productSection, List<Promotion> promotions, int index, bool available)
    {
        Id = id;
        Name = name;
        Price = price;
        UrlImage = urlImage.ToString();
        ProductCategory = productCategory;
        ProductSection = productSection;
        PromotionsJson = System.Text.Json.JsonSerializer.Serialize(promotions);
        Index = index;
        Available = available;
    }
    
    public Product(ProductResponse response)
    {
        Id = response.Id;
        Name = response.Name;
        Price = response.Price;
        UrlImage = response.UrlImage.ToString();
        ProductCategory = response.ProductCategory;
        ProductSection = response.ProductSection;
        PromotionsJson = System.Text.Json.JsonSerializer.Serialize(response.Promotions);
        Index = response.Index;
        Available = response.Available;
    }

    public List<Promotion> GetPromotions()
    {
        if (string.IsNullOrEmpty(PromotionsJson))
            return new List<Promotion>();

        return System.Text.Json.JsonSerializer.Deserialize<List<Promotion>>(PromotionsJson) 
               ?? new List<Promotion>();
    }
}
