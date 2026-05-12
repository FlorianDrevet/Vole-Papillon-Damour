using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.Events.Common;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Events.Commands.RemoveLastNumero;

public record RemoveLastNumeroCommand(
    AssoEventsId AssoEventsId
) : IRequest<ErrorOr<AssoEventResult>>;