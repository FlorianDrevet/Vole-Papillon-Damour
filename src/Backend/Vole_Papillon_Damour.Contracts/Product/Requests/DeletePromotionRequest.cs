namespace Vole_Papillon_Damour.Contracts.Product.Requests;

public class DeletePromotionRequest
{
    public Guid? ProductId { get; init; }
    public int? Quantity { get; init; }
    public double? DiscountedPrice { get; init; }
}