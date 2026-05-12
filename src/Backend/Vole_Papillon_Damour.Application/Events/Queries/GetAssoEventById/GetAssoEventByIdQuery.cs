using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.Events.Common;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Events.Queries.GetAssoEventById;

public record GetAssoEventByIdQuery(
    AssoEventsId AssoEventsId
) : IRequest<ErrorOr<AssoEventResult>>;