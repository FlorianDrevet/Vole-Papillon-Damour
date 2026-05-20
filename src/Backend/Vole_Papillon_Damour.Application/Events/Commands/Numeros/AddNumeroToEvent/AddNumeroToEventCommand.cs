using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.Events.Common;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Events.Commands.Numeros.AddNumeroToEvent;

public record AddNumeroToEventCommand(
    AssoEventsId AssoEventsId,
    int Numero
) : IRequest<ErrorOr<AssoEventResult>>;