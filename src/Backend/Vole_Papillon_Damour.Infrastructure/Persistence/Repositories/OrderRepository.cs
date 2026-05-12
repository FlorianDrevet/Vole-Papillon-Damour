using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Domain.OrderAggregate;
using Vole_Papillon_Damour.Domain.OrderAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Infrastructure.Persistence.Repositories;

public class OrderRepository: BaseRepository<Order, ProjectDbContext>, IOrderRepository
{
    public OrderRepository(ProjectDbContext context) : base(context)
    {
    }

    public async Task<Order?> ChangeStatusAsync(OrderId id, StatusEnum status)
    {
        var order = await Context.Set<Order>().FindAsync(id);

        if (order is null)
            return null;
        
        order.ChangeStatus(status);
        await Context.SaveChangesAsync();
        return order;
    }
}