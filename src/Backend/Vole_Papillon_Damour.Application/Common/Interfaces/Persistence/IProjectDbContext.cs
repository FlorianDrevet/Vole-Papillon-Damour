using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate;
using Vole_Papillon_Damour.Domain.OrderAggregate;
using Vole_Papillon_Damour.Domain.ProductAggregate;
using Vole_Papillon_Damour.Domain.UserAggregate;

namespace Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;

public interface IProjectDbContext
{
    DbSet<Product> Products { get; }
    DbSet<User> Users { get; }
    DbSet<AssoEvents> AssoEvents { get; }
    DbSet<Order> Orders { get; }
}