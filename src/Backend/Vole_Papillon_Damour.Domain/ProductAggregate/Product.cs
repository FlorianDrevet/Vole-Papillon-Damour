using Vole_Papillon_Damour.Domain.Common.Models;
using Vole_Papillon_Damour.Domain.ProductAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Domain.ProductAggregate;

public sealed class Product : AggregateRoot<ProductId>
{
    public string Name { get; private set; } = null!;
    public double Price { get; private set; } = 0;
    public Uri UrlImage { get; private set; } = null!;
    public ProductCategory? ProductCategory { get; set; }
    public ProductSection ProductSection { get; set; } = null!;
    private List<Promotion> _promotions = new();
    public int Index { get; set; } = 0;
    public bool Available { get; set; }

    public IReadOnlyList<Promotion> Promotions => _promotions.AsReadOnly();

    
    private Product(ProductId productId, string name, double price,
        Uri urlImage, bool available, ProductCategory? productCategory, ProductSection productSection) 
        : base(productId)
    {
        Name = name;
        Price = price;
        UrlImage = urlImage;
        ProductCategory = productCategory;
        ProductSection = productSection;
        Available = available;
    }
    
    public static Product Create(string name, double price, Uri urlImage, ProductSection productSection, bool available,
        IEnumerable<Product> allProducts, ProductCategory? productCategory = null)
    {
        var product = new Product(ProductId.CreateUnique(), name, price, urlImage, available,productCategory, productSection);
        //TODO c'est quoi cette merde ?
        product.CalculateIndex(allProducts);
        return product;
    }
    
    public Product(){}

    private void CalculateIndex(IEnumerable<Product> allProducts)
    {
        var productInSameSection = allProducts.Where(p => p.ProductCategory == ProductCategory);
        if (ProductSection.Value == ProductSection.ProductSectionEnum.Bar)
        {
            Index = productInSameSection.Count(pdt => pdt.ProductCategory!.Equals(ProductCategory)) + 1;
        }
        else
        {
            Index = productInSameSection.Count() + 1;
        }
    }

    public bool AddPromotion(Promotion promotion)
    {
        if (_promotions.Find(x => x.Equals(promotion)) is not null)
        {
            return false;
        }
        _promotions.Add(promotion);
        return true;
    }

    public bool DeletePromotion(Promotion promotion)
    {
        return _promotions.Remove(promotion);
    }

    public void Update(string commandName, double commandPrice, Uri urlImagePrincipal, bool available,
        ProductSection commandProductSection, ProductCategory? commandProductCategory)
    {
        Name = commandName;
        Price = commandPrice;
        UrlImage = urlImagePrincipal;
        ProductSection = commandProductSection;
        ProductCategory = commandProductCategory;
        Available = available;
    }
}