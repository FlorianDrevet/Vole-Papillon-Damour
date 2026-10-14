using Microsoft.EntityFrameworkCore;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate.Entities;
using Vole_Papillon_Damour.Domain.EventsAggregate;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Infrastructure.Persistence.Repositories;

public class EventRepository: BaseRepository<AssoEvents, ProjectDbContext>, IEventRepository
{
    public EventRepository(ProjectDbContext context) : base(context)
    {
    }

    public new async Task<AssoEvents> UpdateAsync(AssoEvents entity)
    {
        if (entity.Parties != null)
        {
            Context.Entry(entity).Property(x => x.CurrentPartieIndex).IsModified = true;
            foreach (Partie partie in entity.Parties)
            {
                Context.Entry(partie).Property(x => x.LiveNumeros).IsModified = true;
                Context.Entry(partie).Property(x => x.LastNumeros).IsModified = true;
                Context.Entry(partie).Property(x => x.CurrentLineIndex).IsModified = true;
            }
        }

        return await base.UpdateAsync(entity);
    }

    public async Task<AssoEvents?> GetNextBingoAsync()
    {
        var today = DateTimeOffset.Now.Date; // Obtient la date actuelle sans l'heure

        return (await Context.AssoEvents
                .Where(x => !x.IsCancelled && x.DateStart.Date >= today) // Comparaison sans tenir compte de l'heure
                .OrderBy(x => x.DateStart)
                .ToListAsync())
            .FirstOrDefault(x => x.EventsType.Value == EventsType.EventsTypeEnum.Bingo);
    }
    
    public async Task<AssoEvents?> GetNextBooksAsync()
    {
        var today = DateTimeOffset.Now.Date; // Obtient la date actuelle sans l'heure
        
        return (await Context.AssoEvents
                .Where(x => !x.IsCancelled && (x.DateStart >= today || x.DateEnd >= today))
                .OrderBy(x => x.DateStart)
                .ToListAsync())
            .FirstOrDefault(x => x.EventsType.Value == EventsType.EventsTypeEnum.Books);
    }
    
    public async Task<List<AssoEvents>> GetNextOtherAsync()
    {
        var today = DateTimeOffset.Now.Date; // Obtient la date actuelle sans l'heure
        
        return (await Context.AssoEvents
                .Where(x => !x.IsCancelled && (x.DateStart >= today || x.DateEnd >= today))
                .OrderBy(x => x.DateStart)
                .ToListAsync())
            .FindAll(x => 
                x.EventsType.Value == EventsType.EventsTypeEnum.Other);
    }
    
    public async Task<List<AssoEvents>> GetNextEventsAsync()
    {
        var today = DateTimeOffset.Now.Date; // Obtient la date actuelle sans l'heure
        
        return await Context.AssoEvents
            .Where(x => !x.IsCancelled && (x.DateStart >= today || x.DateEnd >= today))
            .OrderBy(x => x.DateStart)
            .ToListAsync();
    }

    public async Task<List<AssoEvents>> GetNotCancelledFromAsync(
        DateTimeOffset fromUtc,
        CancellationToken cancellationToken)
    {
        return await Context.AssoEvents
            .Where(x => !x.IsCancelled && (x.DateStart >= fromUtc || x.DateEnd >= fromUtc))
            .OrderBy(x => x.DateStart)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<AssoEventsId, int>> GetLiveRevisionsAsync(
        IReadOnlyCollection<AssoEventsId> ids,
        CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return new Dictionary<AssoEventsId, int>();
        }

        var requestedIds = ids.ToList();
        var revisions = await Context.AssoEvents
            .AsNoTracking()
            .Where(x => requestedIds.Contains(x.Id))
            .Select(x => new { x.Id, x.LiveRevision })
            .ToListAsync(cancellationToken);

        return revisions.ToDictionary(x => x.Id, x => x.LiveRevision);
    }
}
