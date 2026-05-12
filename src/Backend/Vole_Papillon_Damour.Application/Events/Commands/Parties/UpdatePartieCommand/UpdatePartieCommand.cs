using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.Events.Common;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Events.Commands.Events.Partie.UpdatePartieCommand;

public record UpdatePartieCommand(
    AssoEventsId AssoEventsId,
    PartieId PartieId,
    string Name,
    PartieType PartieType,
    bool PauseAfter
) : IRequest<ErrorOr<AssoEventResult>>;