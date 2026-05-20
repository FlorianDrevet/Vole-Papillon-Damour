using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence.BaseRepository;
using Vole_Papillon_Damour.Domain.Common.Errors;
using Vole_Papillon_Damour.Domain.ProductAggregate;
using Vole_Papillon_Damour.Domain.ProductAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;

public interface IProductRepository: IRepository<Product>
{
    //TODO
    //public List<Product> AllProductsOfCategory(ProductCategory category);
}