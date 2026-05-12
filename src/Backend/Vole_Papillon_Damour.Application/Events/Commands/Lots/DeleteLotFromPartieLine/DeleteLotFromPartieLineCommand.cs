using ErrorOr;
using MediatR;
using Vole_Papillon_Damour.Application.Events.Common;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate.ValueObjects;
using Vole_Papillon_Damour.Domain.EventsAggregate.ValueObjects;

namespace Vole_Papillon_Damour.Application.Events.Commands.Lots.DeleteLotFromPartieLine;

public class DeleteLotFromPartieLineCommand : IRequest<ErrorOr<AssoEventResult>>
{
    public AssoEventsId AssoEventsId { get; init; }
    public PartieId PartieId { get; init; }
    public LinePartieId LinePartieId { get; init; }
    public LotId LotId { get; init; }
}