using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence.BaseRepository;
using Vole_Papillon_Damour.Domain.ActualityAggregate;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;

public interface IActualityRepository: IRepository<Domain.ActualityAggregate.Actuality>
{
    Task<List<Domain.ActualityAggregate.Actuality>> GetLatestActualityAsync();
}