using ShopAppVpd.Services.Responses;

namespace ShopAppVpd.Apis.Responses;

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