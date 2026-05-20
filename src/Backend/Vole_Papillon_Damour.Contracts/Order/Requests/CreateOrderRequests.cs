namespace Vole_Papillon_Damour.Contracts.Order.Requests;

public class CreateOrderRequests
{
    public string FamilyName {get; set;}
    public string Status {get; set;}
    public double TotalPrice {get; set;}
    public List<CreateOrderProductRequest> OrderedProduct {get; set;}
}

public class CreateOrderProductRequest
{
    public Guid ProductId {get; set;}
    public int Quantity {get; set;}
}