using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Domain.ProductAggregate;
using Vole_Papillon_Damour.Domain.ProductAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Infrastructure.Persistence.Repositories;

public class ProductRepository: BaseRepository<Product, ProjectDbContext>, IProductRepository
{
    public ProductRepository(ProjectDbContext context) : base(context)
    {
    }

    public async Task<Product> AddPromotionAsync(Product product, Promotion promotion)
    {
        product.AddPromotion(promotion);
        await Context.SaveChangesAsync();
        return product;
    }

    public async Task<Product> RemovePromotionAsync(Product product, Promotion promotion)
    {
        await Context.SaveChangesAsync();
        return product;
    }
}