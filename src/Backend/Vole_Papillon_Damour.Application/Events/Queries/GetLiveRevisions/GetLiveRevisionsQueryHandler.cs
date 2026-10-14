using MediatR;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Events.Queries.GetLiveRevisions;

public class GetLiveRevisionsQueryHandler(IEventRepository eventRepository)
    : IRequestHandler<GetLiveRevisionsQuery, IReadOnlyDictionary<AssoEventsId, int>>
{
    public Task<IReadOnlyDictionary<AssoEventsId, int>> Handle(
        GetLiveRevisionsQuery query,
        CancellationToken cancellationToken) =>
        eventRepository.GetLiveRevisionsAsync(query.AssoEventsIds, cancellationToken);
}
