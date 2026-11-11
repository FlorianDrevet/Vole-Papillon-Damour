namespace ShopAppVpd.Dtos;

public sealed record ProductCatalog(IEnumerable<Product> Products, bool IsFromCache);
