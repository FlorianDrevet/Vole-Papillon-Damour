using ErrorOr;
using MapsterMapper;
using MediatR;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Common.Interfaces.Services;
using Vole_Papillon_Damour.Application.Events.Commands.Lots.CreateLot;
using Vole_Papillon_Damour.Application.Events.Common;
using Vole_Papillon_Damour.Domain.AssoEventsAggregate.Entities;
using Vole_Papillon_Damour.Domain.Common.Errors;

namespace Vole_Papillon_Damour.Application.Events.Commands.Lots.DeleteLotFromPartieLine;

public class DeleteLotFromPartieLineCommandHandler(IEventRepository eventRepository, IBlobService blobService, IMapper mapper)
    : IRequestHandler<DeleteLotFromPartieLineCommand, ErrorOr<AssoEventResult>>
{
    public async Task<ErrorOr<AssoEventResult>> Handle(DeleteLotFromPartieLineCommand lineCommand, CancellationToken cancellationToken)
    {
        var assoEvent = await eventRepository.GetByIdAsync(lineCommand.AssoEventsId);
        if (assoEvent is null)
        {
            return Errors.AssoEvent.AssoEventNotFound(lineCommand.AssoEventsId);
        }

        var parties = assoEvent.Parties?.ToList().Find(p => p.Id == lineCommand.PartieId);
        if (parties is null)
        {
            return Errors.AssoEvent.Partie.PartieNotFound(lineCommand.AssoEventsId, lineCommand.PartieId);
        }

        var linePartie = parties.LineParties.ToList().Find(lp => lp.Id == lineCommand.LinePartieId);
        if (linePartie is null)
        {
            return Errors.AssoEvent.Partie.LinePartie.PartieLineNotFound(lineCommand.AssoEventsId, lineCommand.PartieId, lineCommand.LinePartieId);
        }

        if (!linePartie.DeleteLot(lineCommand.LotId))
        {
            return Errors.AssoEvent.Partie.LinePartie.Lot.LotNotFound(
                lineCommand.AssoEventsId,
                lineCommand.PartieId,
                lineCommand.LinePartieId,
                lineCommand.LotId
                );
        }
        
        await eventRepository.UpdateAsync(assoEvent);

        return mapper.Map<AssoEventResult>(assoEvent);
    }
}