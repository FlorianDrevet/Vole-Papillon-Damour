using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.Events.Common;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Events.Commands.Parties.ChangeIndexPartie;

public record ChangeIndexPartieCommand(
    AssoEventsId AssoEventsId,
    PartieId PartieId,
    int Index
) : IRequest<ErrorOr<AssoEventResult>>;