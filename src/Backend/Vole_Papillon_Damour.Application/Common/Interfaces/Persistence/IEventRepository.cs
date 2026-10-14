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

    /// <summary>Events not cancelled that start or end at or after <paramref name="fromUtc"/>.</summary>
    Task<List<AssoEvents>> GetNotCancelledFromAsync(DateTimeOffset fromUtc, CancellationToken cancellationToken);

    /// <summary>Current <c>LiveRevision</c> of each existing event among <paramref name="ids"/>.</summary>
    Task<IReadOnlyDictionary<AssoEventsId, int>> GetLiveRevisionsAsync(
        IReadOnlyCollection<AssoEventsId> ids,
        CancellationToken cancellationToken);
}
