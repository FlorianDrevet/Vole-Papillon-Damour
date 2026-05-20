using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence.BaseRepository;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;

public interface IEventRepository: IRepository<AssoEvents>
{
    Task<AssoEvents?> GetNextBingoAsync();
    Task<AssoEvents?> GetNextBooksAsync();
    Task<List<AssoEvents>> GetNextOtherAsync();
    Task<List<AssoEvents>> GetNextEventsAsync();
}