using Vole_Papillon_Damour.Domain.ProductAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Contracts.Product.Responses;

public record ProductResponse(
    Guid Id,
    string Name,
    double Price,
    Uri UrlImage,
    string? ProductCategory,
    string ProductSection,
    List<PromotionResponse> Promotions,
    int Index,
    bool Available
    );
    
public record PromotionResponse(
    int Quantity,
    double DiscountedPrice);