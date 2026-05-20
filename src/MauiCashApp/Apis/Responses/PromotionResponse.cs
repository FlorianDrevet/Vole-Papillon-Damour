namespace ShopAppVpd.Services.Responses;

public record PromotionResponse(
    int Quantity,
    double DiscountedPrice);