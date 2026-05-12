using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Events.Commands.Events.DeleteEvent;

public record DeleteEventCommand(
   AssoEventsId EventId 
) : IRequest<ErrorOr<bool>>;