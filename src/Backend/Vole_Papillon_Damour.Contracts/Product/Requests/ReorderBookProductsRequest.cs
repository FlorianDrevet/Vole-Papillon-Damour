namespace Vole_Papillon_Damour.Contracts.Product.Requests;

public sealed record ReorderBookProductsRequest(IReadOnlyList<Guid> ProductIds);
