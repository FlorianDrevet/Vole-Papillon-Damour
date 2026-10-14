using MediatR;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Events.Queries.GetLiveRevisions;

public record GetLiveRevisionsQuery(IReadOnlyCollection<AssoEventsId> AssoEventsIds)
    : IRequest<IReadOnlyDictionary<AssoEventsId, int>>;
