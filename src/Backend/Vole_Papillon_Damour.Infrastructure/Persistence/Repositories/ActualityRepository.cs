using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Domain.ActualityAggregate;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate.Entities;
using Vole_Papillon_Damour.Domain.EventsAggregate;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Infrastructure.Persistence.Repositories;

public class ActualityRepository: BaseRepository<Actuality, ProjectDbContext>, IActualityRepository
{
    public ActualityRepository(ProjectDbContext context) : base(context)
    {
    }

    public Task<List<Actuality>> GetLatestActualityAsync()
    {
        return Context.Actualities.OrderByDescending(x => x.Date).Take(3).ToListAsync();
    }
}