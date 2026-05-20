using ErrorOr;
using MapsterMapper;
using MediatR;
using Vole_Papillon_Damour.Application.Common.Interfaces.Persistence;
using Vole_Papillon_Damour.Application.Events.Common;
using Vole_Papillon_Damour.Domain.Common.Errors;

namespace Vole_Papillon_Damour.Application.Events.Commands.LineParties.DeleteLinePartieFromPartie;

public class DeleteLinePartieFromPartieCommandHandler(IEventRepository eventRepository, IMapper mapper)
    : IRequestHandler<DeleteLinePartieFromPartieCommand, ErrorOr<AssoEventResult>>
{
    public async Task<ErrorOr<AssoEventResult>> Handle(DeleteLinePartieFromPartieCommand command, CancellationToken cancellationToken)
    {
        var assoEvent = await eventRepository.GetByIdAsync(command.AssoEventsId);
        if (assoEvent is null)
        {
            return Errors.AssoEvent.AssoEventNotFound(command.AssoEventsId);
        }

        var partie = assoEvent.Parties?.ToList().Find(p => p.Id == command.PartieId);
        if (partie is null)
        {
            return Errors.AssoEvent.Partie.PartieNotFound(command.AssoEventsId, command.PartieId);
        }

        if (partie.DeleteLinePartie(command.LinePartieId))
        {
            return Errors.AssoEvent.Partie.LinePartie.PartieLineNotFound(command.AssoEventsId, command.PartieId, command.LinePartieId);
        }

        await eventRepository.UpdateAsync(assoEvent);

        return mapper.Map<AssoEventResult>(assoEvent);
    }
}