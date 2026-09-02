using Vole_Papillon_Damour.Domain.ProductAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Products.Common;

public record ProductResult(
    ProductId Id,
    string Name,
    double Price,
    Uri UrlImage,
    ProductCategory? ProductCategory,
    ProductSection ProductSection,
    List<PromotionResult> Promotions,
    int Index,
    bool Available,
    bool VisibleOnWebsite
);

public record PromotionResult(
    int Quantity,
    double DiscountedPrice);
